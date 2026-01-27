# Task 1: Idempotent Consumer Pattern

**Priority:** High
**Effort:** Low-Medium
**Risk if skipped:** Duplicate order payments, duplicate emails, duplicate status updates

---

## Problem

SQS guarantees **at-least-once** delivery, not exactly-once. Messages can be delivered more than once due to:

- Network retries
- Visibility timeout expiry before processing completes
- SQS internal retry mechanisms

Currently, none of our event handlers check for duplicate messages:

```csharp
// PaymentEventHandler.cs - Current (no dedup)
public async Task<bool> HandleAsync(PaymentEventMessage evt, CancellationToken ct)
{
    // If this runs twice for the same event:
    // → Order status updated twice (harmless but wasteful)
    // → OR worse: payment processed twice
    var command = new ConfirmOrderCommand(evt.OrderId, ...);
    await _mediator.Send(command, ct);
    return true;
}
```

## Solution

Use `EventEnvelope.EventId` (already present in every message) to track which events have been processed.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  IEventHandler<T>.HandleAsync()                              │
│                                                              │
│  1. Check: Has EventId been processed?                       │
│     ┌──────────────────────────────────────────────────────┐ │
│     │ DynamoDB / PostgreSQL / Redis                         │ │
│     │ ProcessedEvents table                                │ │
│     │                                                      │ │
│     │ PK: EventId                                          │ │
│     │ ProcessedAt: DateTime                                │ │
│     │ ServiceName: string                                  │ │
│     └──────────────────────────────────────────────────────┘ │
│                                                              │
│  2. If EXISTS → return true (skip, delete from queue)        │
│                                                              │
│  3. If NOT EXISTS →                                          │
│     a. Process event (business logic)                        │
│     b. Insert EventId into ProcessedEvents                   │
│     c. return true                                           │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Plan

#### Step 1: Add EventId to Inbound Message Models

Currently `EventId` is in the envelope but not passed to handlers. Two options:

**Option A:** Add `EventId` to the enricher function (like `EventType`):
```csharp
// Update SqsEventQueue<T> to support EventId enrichment
eventIdEnricher: (msg, eventId) => msg with { EventId = eventId }
```

**Option B:** Add `EventId` as a property on `QueueMessage<T>`:
```csharp
public class QueueMessage<T>
{
    public string MessageId { get; set; }
    public string ReceiptHandle { get; set; }
    public string EventId { get; set; }    // ← Add this
    public T Body { get; set; }
}
```

Option B is cleaner since EventId is metadata, not business data.

#### Step 2: Create Idempotency Check Interface (SharedKernel)

```csharp
// gearify-shared-kernel/Messaging/IIdempotencyStore.cs
public interface IIdempotencyStore
{
    Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default);
    Task MarkAsProcessedAsync(string eventId, CancellationToken ct = default);
}
```

#### Step 3: Implement Storage Backend

**Option A: DynamoDB (recommended for services already using DynamoDB)**
```csharp
public class DynamoDbIdempotencyStore : IIdempotencyStore
{
    // Table: ProcessedEvents
    // PK: EventId (string)
    // Attributes: ProcessedAt, ServiceName
    // TTL: 7 days (auto-cleanup)
}
```

**Option B: PostgreSQL (for Order/Payment services)**
```sql
CREATE TABLE processed_events (
    event_id VARCHAR(255) PRIMARY KEY,
    service_name VARCHAR(100) NOT NULL,
    processed_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Auto-cleanup: delete events older than 7 days
CREATE INDEX idx_processed_events_date ON processed_events(processed_at);
```

**Option C: Redis (fastest, if already available)**
```csharp
// SET eventId "1" EX 604800  (7 day TTL)
await _redis.StringSetAsync(eventId, "1", TimeSpan.FromDays(7));
```

#### Step 4: Integrate into EventQueueProcessor or Handlers

**Option A: In EventQueueProcessor (transparent to handlers):**
```csharp
// EventQueueProcessor<T> - automatic dedup
foreach (var message in messages)
{
    if (await _idempotencyStore.HasBeenProcessedAsync(message.EventId))
    {
        await _queue.DeleteMessageAsync(message.ReceiptHandle);
        continue;  // Skip duplicate
    }

    var success = await _handler.HandleAsync(message.Body, ct);
    if (success)
    {
        await _idempotencyStore.MarkAsProcessedAsync(message.EventId);
        await _queue.DeleteMessageAsync(message.ReceiptHandle);
    }
}
```

**Option B: In each handler (more control):**
```csharp
public async Task<bool> HandleAsync(PaymentEventMessage evt, CancellationToken ct)
{
    if (await _idempotencyStore.HasBeenProcessedAsync(evt.EventId))
        return true;

    // ... business logic ...

    await _idempotencyStore.MarkAsProcessedAsync(evt.EventId);
    return true;
}
```

### Affected Services

| Service | Handler | Risk Without Idempotency |
|---------|---------|--------------------------|
| Order Service | `PaymentEventHandler` | Order status updated twice |
| Payment Service | `OrderCreatedEventHandler` | Payment processed twice (critical!) |
| Notification Service | `PaymentFailedEventHandler` | Duplicate failure emails sent |
| Media Service | `ImageProcessingEventHandler` | Image variants generated twice (wasteful) |
| Catalog Service | `ImageProcessingCompletedEventHandler` | Thumbnail URL updated twice (harmless) |
| Search Service | `CatalogEventHandler` | Product indexed twice (harmless, upsert) |

### Files to Create/Modify

| Action | File |
|--------|------|
| Create | `gearify-shared-kernel/Messaging/IIdempotencyStore.cs` |
| Create | `gearify-shared-kernel/Messaging/DynamoDbIdempotencyStore.cs` (or PostgreSQL) |
| Modify | `gearify-shared-kernel/Messaging/QueueMessage.cs` (add EventId) |
| Modify | `gearify-shared-kernel/Messaging/SqsEventQueue.cs` (extract EventId from envelope) |
| Modify | `gearify-shared-kernel/Messaging/EventQueueProcessor.cs` (add dedup check) |
| Modify | Each service's `Startup.cs` (register IIdempotencyStore) |

### Acceptance Criteria

- [ ] `EventId` is accessible in handlers or processor
- [ ] Duplicate messages are detected and skipped
- [ ] Processed events auto-expire after 7 days (TTL)
- [ ] Logging shows when a duplicate is skipped
- [ ] All 6 consumer services have idempotency enabled
