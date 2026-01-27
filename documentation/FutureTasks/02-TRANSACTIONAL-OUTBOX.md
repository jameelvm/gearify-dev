# Task 2: Transactional Outbox Pattern

**Priority:** High
**Effort:** Medium
**Risk if skipped:** Events lost when SNS is unavailable, data inconsistency between services

---

## Problem

Current pattern saves to database then publishes to SNS as two separate operations:

```csharp
// CreateOrderCommandHandler.cs - Current
public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
{
    var order = Order.Create(...);
    await _repository.SaveAsync(order);              // ✅ Step 1: DB succeeds

    await _eventPublisher.PublishAsync(                // ❌ Step 2: SNS might fail!
        new OrderCreatedEvent(...));

    return OrderMapper.ToDto(order);
}
```

**Failure scenarios:**

```
Scenario 1: SNS is temporarily down
──────────────────────────────────────
  DB Save     → ✅ Order created in PostgreSQL
  SNS Publish → ❌ Connection refused
  Result      → Order exists but Payment Service never knows
                Customer is stuck with "Pending" order forever

Scenario 2: Service crashes after DB save
──────────────────────────────────────
  DB Save     → ✅ Order persisted
  (crash)     → ❌ Service restarts
  SNS Publish → Never happens
  Result      → Same: orphaned order

Scenario 3: SNS succeeds but DB didn't actually commit
──────────────────────────────────────
  DB Save     → ❌ Transaction rolls back (timeout, deadlock)
  SNS Publish → ✅ Event published
  Result      → Payment Service processes non-existent order
```

## Solution: Transactional Outbox

Save the event to an **OutboxMessages** table in the **same database transaction** as the business data. A background publisher polls the outbox and publishes to SNS.

### Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  CreateOrderCommandHandler                                                │
│                                                                           │
│  BEGIN TRANSACTION                                                        │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                                                                     │  │
│  │  1. INSERT INTO orders (...)  VALUES (...)                          │  │
│  │  2. INSERT INTO outbox_messages (event_type, payload, ...) VALUES   │  │
│  │                                                                     │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│  COMMIT  ← Both succeed or both fail                                     │
│                                                                           │
│  No SNS publish here!                                                    │
└──────────────────────────────────────────────────────────────────────────┘

                              ▼

┌──────────────────────────────────────────────────────────────────────────┐
│  OutboxPublisher (BackgroundService)                                      │
│                                                                           │
│  Loop every 5 seconds:                                                    │
│  1. SELECT * FROM outbox_messages WHERE published_at IS NULL              │
│     ORDER BY created_at LIMIT 100                                         │
│                                                                           │
│  2. For each message:                                                     │
│     a. Publish to SNS via ISnsEventPublisher                              │
│     b. UPDATE outbox_messages SET published_at = NOW() WHERE id = @id     │
│                                                                           │
│  3. Periodically clean up old published messages (> 7 days)               │
│                                                                           │
│  On failure: message stays unpublished, retried next cycle                │
└──────────────────────────────────────────────────────────────────────────┘
```

### Implementation Plan

#### Step 1: Create OutboxMessage Table (PostgreSQL)

For services using PostgreSQL (Order Service):

```sql
CREATE TABLE outbox_messages (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_type      VARCHAR(255) NOT NULL,
    event_id        VARCHAR(255) NOT NULL,
    tenant_id       VARCHAR(255) NOT NULL,
    payload         JSONB NOT NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT NOW(),
    published_at    TIMESTAMP NULL,
    retry_count     INT NOT NULL DEFAULT 0,
    last_error      TEXT NULL
);

CREATE INDEX idx_outbox_unpublished ON outbox_messages(created_at)
    WHERE published_at IS NULL;
```

For services using DynamoDB (Catalog, Media):

```
Table: OutboxMessages
PK: Id (UUID string)
Attributes: EventType, EventId, TenantId, Payload, CreatedAt, PublishedAt
GSI: UnpublishedIndex (PK: Status="PENDING", SK: CreatedAt)
TTL: ExpiresAt (7 days after PublishedAt)
```

#### Step 2: Create SharedKernel Interfaces

```csharp
// gearify-shared-kernel/Messaging/Outbox/IOutboxStore.cs
public interface IOutboxStore
{
    Task SaveAsync(OutboxMessage message, CancellationToken ct = default);
    Task<List<OutboxMessage>> GetUnpublishedAsync(int limit = 100, CancellationToken ct = default);
    Task MarkAsPublishedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken ct = default);
    Task CleanupOldMessagesAsync(TimeSpan olderThan, CancellationToken ct = default);
}

// gearify-shared-kernel/Messaging/Outbox/OutboxMessage.cs
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; }
    public string EventId { get; set; }
    public string TenantId { get; set; }
    public string Payload { get; set; }       // JSON serialized domain event
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
```

#### Step 3: Create OutboxPublisher (SharedKernel)

```csharp
// gearify-shared-kernel/Messaging/Outbox/OutboxPublisher.cs
public class OutboxPublisher : BackgroundService
{
    // Polls IOutboxStore every 5 seconds
    // Publishes unpublished messages to SNS
    // Marks as published on success
    // Increments retry count on failure
    // Cleans up messages older than 7 days
}
```

#### Step 4: Update Command Handlers

```csharp
// Before (current)
await _repository.SaveAsync(order);
await _eventPublisher.PublishAsync(new OrderCreatedEvent(...));

// After (with outbox)
await _unitOfWork.BeginAsync();
await _repository.SaveAsync(order);
await _outboxStore.SaveAsync(new OutboxMessage
{
    EventType = nameof(OrderCreatedEvent),
    EventId = Guid.NewGuid().ToString(),
    TenantId = tenantId,
    Payload = JsonSerializer.Serialize(new OrderCreatedEvent(...))
});
await _unitOfWork.CommitAsync();
// SNS publish happens asynchronously via OutboxPublisher
```

#### Alternative: Use MassTransit

Instead of building a custom outbox, consider [MassTransit](https://masstransit.io/) which provides:
- Built-in transactional outbox for EF Core + DynamoDB
- Built-in SNS/SQS transport
- Message retry, redelivery, fault handling
- Saga state machines

```csharp
// MassTransit Outbox with EF Core
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();       // or UsePostgres()
        o.UseBusOutbox();
    });

    x.UsingAmazonSqs((context, cfg) =>
    {
        cfg.Host("us-east-1", h => { });
        cfg.ConfigureEndpoints(context);
    });
});
```

### Affected Services (in priority order)

| Service | Database | Critical Events | Risk |
|---------|----------|-----------------|------|
| **Order Service** | PostgreSQL | `OrderCreatedEvent` | Payment never triggered |
| **Payment Service** | PostgreSQL | `PaymentCompletedEvent`, `PaymentFailedEvent` | Order stuck in Pending |
| **Catalog Service** | DynamoDB | `ProductCreatedEvent`, `ProductUpdatedEvent` | Search index out of sync |
| **Media Service** | DynamoDB | `MediaUploadedEvent`, `ImageProcessingCompletedEvent` | Thumbnails never generated |

### Files to Create/Modify

| Action | File |
|--------|------|
| Create | `gearify-shared-kernel/Messaging/Outbox/IOutboxStore.cs` |
| Create | `gearify-shared-kernel/Messaging/Outbox/OutboxMessage.cs` |
| Create | `gearify-shared-kernel/Messaging/Outbox/OutboxPublisher.cs` |
| Create | `gearify-order-svc/Infrastructure/Data/OutboxMessages` (EF Core entity + migration) |
| Modify | `gearify-order-svc/Application/Commands/CreateOrderCommandHandler.cs` |
| Modify | `gearify-payment-svc/Application/Commands/ProcessOrderPaymentCommandHandler.cs` |
| Modify | Each service's `Startup.cs` (register outbox services) |

### Acceptance Criteria

- [ ] Events are saved in the same transaction as business data
- [ ] OutboxPublisher reliably publishes pending messages to SNS
- [ ] Failed publishes are retried with exponential backoff
- [ ] Old published messages are cleaned up automatically
- [ ] If SNS is down, events queue up and publish when SNS recovers
- [ ] No events are lost even if the service crashes after DB commit
