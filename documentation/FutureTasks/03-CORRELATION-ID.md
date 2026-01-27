# Task 3: Correlation ID & Distributed Tracing

**Priority:** Medium
**Effort:** Low-Medium
**Risk if skipped:** Impossible to trace a request across services, debugging production issues becomes guesswork

---

## Problem

When a user initiates checkout, the request flows through multiple services:

```
Frontend → API Gateway → Order Service → (SNS) → Payment Service → (SNS) → Order Service
                                                                          → Notification Service
                                                                          → Shipping Service
```

Currently, each service logs independently with no way to correlate logs across services:

```
// Order Service logs
[2024-01-15 10:00:01] INFO  Creating order for user U123
[2024-01-15 10:00:02] INFO  Order ORD-456 created

// Payment Service logs
[2024-01-15 10:00:03] INFO  Processing payment for order ORD-456
[2024-01-15 10:00:05] ERROR Payment failed: Card declined

// Notification Service logs
[2024-01-15 10:00:06] INFO  Sending payment failed email
```

**Problems:**
- Which user request triggered the payment failure?
- How long did the entire checkout take end-to-end?
- Did the notification actually correspond to the same payment failure?
- If two orders are processing simultaneously, which logs belong to which flow?

## Solution: Correlation ID

Generate a unique `CorrelationId` at the entry point (API Gateway or first service) and propagate it through every HTTP request and event message across all services.

### Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  API Gateway (or first service to receive request)                       │
│                                                                          │
│  1. Check for X-Correlation-Id header                                    │
│     - If present: use it                                                 │
│     - If absent:  generate new UUID                                      │
│                                                                          │
│  2. Add to HttpContext / AsyncLocal<string>                              │
│  3. Forward header to downstream services                                │
│  4. Include in all log entries                                           │
└──────────────────────────────────────────────────────────────────────────┘

                    ▼ HTTP (X-Correlation-Id header)

┌──────────────────────────────────────────────────────────────────────────┐
│  Order Service                                                           │
│                                                                          │
│  1. Extract X-Correlation-Id from incoming request                       │
│  2. Store in AsyncLocal / HttpContext.Items                              │
│  3. Include in all log entries (structured logging scope)                │
│  4. Include in EventEnvelope when publishing to SNS                      │
│     EventEnvelope {                                                      │
│       EventId: "...",                                                    │
│       CorrelationId: "abc-123",   ← NEW FIELD                           │
│       EventType: "OrderCreated",                                         │
│       Payload: { ... }                                                   │
│     }                                                                    │
└──────────────────────────────────────────────────────────────────────────┘

                    ▼ SNS/SQS (CorrelationId in EventEnvelope)

┌──────────────────────────────────────────────────────────────────────────┐
│  Payment Service                                                         │
│                                                                          │
│  1. Extract CorrelationId from EventEnvelope                             │
│  2. Store in AsyncLocal for duration of message processing               │
│  3. Include in all log entries                                           │
│  4. Include in outbound EventEnvelope when publishing PaymentCompleted   │
└──────────────────────────────────────────────────────────────────────────┘
```

### Implementation Plan

#### Step 1: Add CorrelationId to EventEnvelope

```csharp
// gearify-shared-kernel/Messaging/EventEnvelope.cs - Updated
public class EventEnvelope
{
    public string EventId { get; set; }
    public string EventType { get; set; }
    public string TenantId { get; set; }
    public string CorrelationId { get; set; }   // ← NEW
    public DateTime Timestamp { get; set; }
    public object Payload { get; set; }
}
```

#### Step 2: Create CorrelationContext (SharedKernel)

```csharp
// gearify-shared-kernel/Correlation/CorrelationContext.cs
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public static string? CorrelationId
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Gets the current CorrelationId or generates a new one.
    /// </summary>
    public static string GetOrCreate()
    {
        if (string.IsNullOrEmpty(_correlationId.Value))
            _correlationId.Value = Guid.NewGuid().ToString();
        return _correlationId.Value;
    }
}
```

#### Step 3: Create HTTP Middleware (SharedKernel)

```csharp
// gearify-shared-kernel/Correlation/CorrelationIdMiddleware.cs
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract from incoming request or generate new
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        // Store in context
        CorrelationContext.CorrelationId = correlationId;
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers (for client debugging)
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Add to logging scope (Serilog / Microsoft.Extensions.Logging)
        using (context.RequestServices
            .GetRequiredService<ILogger<CorrelationIdMiddleware>>()
            .BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
        {
            await _next(context);
        }
    }
}
```

#### Step 4: Update SnsEventPublisherBase

```csharp
// gearify-shared-kernel/Messaging/SnsEventPublisherBase.cs - Updated
protected EventEnvelope CreateEnvelope(string eventType, string tenantId, object payload)
{
    return new EventEnvelope
    {
        EventId = Guid.NewGuid().ToString(),
        EventType = eventType,
        TenantId = tenantId,
        CorrelationId = CorrelationContext.GetOrCreate(),  // ← NEW
        Timestamp = DateTime.UtcNow,
        Payload = payload
    };
}
```

#### Step 5: Update SqsEventQueue to Extract CorrelationId

```csharp
// In SqsEventQueue<T>.ReceiveMessagesAsync()
// After deserializing the EventEnvelope:
var correlationId = envelope.CorrelationId;

// Set context for the processing thread
CorrelationContext.CorrelationId = correlationId;

// Optionally enrich the message with CorrelationId
```

#### Step 6: Update EventQueueProcessor to Set Logging Scope

```csharp
// gearify-shared-kernel/Messaging/EventQueueProcessor.cs - Updated
foreach (var message in messages)
{
    // CorrelationId was set by SqsEventQueue when deserializing
    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = CorrelationContext.CorrelationId ?? "unknown"
    }))
    {
        var success = await _handler.HandleAsync(message.Body, ct);
        // ...
    }
}
```

#### Step 7: Register Middleware in Each Service

```csharp
// Each service's Startup.cs or Program.cs
app.UseMiddleware<CorrelationIdMiddleware>();  // Add early in pipeline
```

### Propagation Matrix

| From | To | Mechanism |
|------|----|-----------|
| Frontend → API Gateway | HTTP | `X-Correlation-Id` header |
| API Gateway → Services | HTTP | `X-Correlation-Id` header (YARP forwards headers) |
| Service → SNS | Event | `EventEnvelope.CorrelationId` field |
| SNS → SQS → Service | Event | Extracted from `EventEnvelope.CorrelationId` |
| Service → Service (HTTP) | HTTP | `X-Correlation-Id` header via `HttpClient` DelegatingHandler |
| Service → Logs | Structured Log | `CorrelationId` property in log scope |

### Log Output Example (After Implementation)

```
// Order Service
[2024-01-15 10:00:01] INFO  [CorrelationId=abc-123] Creating order for user U123
[2024-01-15 10:00:02] INFO  [CorrelationId=abc-123] Order ORD-456 created, publishing OrderCreatedEvent

// Payment Service
[2024-01-15 10:00:03] INFO  [CorrelationId=abc-123] Received OrderCreatedEvent for order ORD-456
[2024-01-15 10:00:05] ERROR [CorrelationId=abc-123] Payment failed: Card declined

// Notification Service
[2024-01-15 10:00:06] INFO  [CorrelationId=abc-123] Sending payment failed email for order ORD-456

// Now you can: grep "abc-123" across all service logs to see the full flow
```

### Optional: HttpClient Propagation

For service-to-service HTTP calls (not just events):

```csharp
// gearify-shared-kernel/Correlation/CorrelationIdDelegatingHandler.cs
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var correlationId = CorrelationContext.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        return base.SendAsync(request, ct);
    }
}

// Registration
services.AddHttpClient("downstream")
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
```

### Affected Services

| Service | Changes Needed |
|---------|---------------|
| **SharedKernel** | Add `CorrelationContext`, `CorrelationIdMiddleware`, update `EventEnvelope`, `SnsEventPublisherBase`, `SqsEventQueue<T>`, `EventQueueProcessor<T>` |
| **All 6+ services** | Register middleware in `Startup.cs` |
| **API Gateway** | YARP already forwards headers — verify `X-Correlation-Id` passes through |

### Files to Create/Modify

| Action | File |
|--------|------|
| Create | `gearify-shared-kernel/Correlation/CorrelationContext.cs` |
| Create | `gearify-shared-kernel/Correlation/CorrelationIdMiddleware.cs` |
| Create | `gearify-shared-kernel/Correlation/CorrelationIdDelegatingHandler.cs` |
| Modify | `gearify-shared-kernel/Messaging/EventEnvelope.cs` (add CorrelationId) |
| Modify | `gearify-shared-kernel/Messaging/SnsEventPublisherBase.cs` (include CorrelationId) |
| Modify | `gearify-shared-kernel/Messaging/SqsEventQueue.cs` (extract CorrelationId) |
| Modify | `gearify-shared-kernel/Messaging/EventQueueProcessor.cs` (logging scope) |
| Modify | Each service's `Startup.cs` (register middleware) |

### Acceptance Criteria

- [ ] Every HTTP request has a `X-Correlation-Id` header (generated if missing)
- [ ] Every `EventEnvelope` includes `CorrelationId`
- [ ] All log entries within a request/event processing chain share the same `CorrelationId`
- [ ] CorrelationId is returned in HTTP response headers
- [ ] Searching logs by CorrelationId shows the complete flow across services
- [ ] Service-to-service HTTP calls propagate the CorrelationId header
