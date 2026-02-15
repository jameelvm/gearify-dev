# Task 2: Transactional Outbox Pattern

**Priority:** High
**Effort:** Medium
**Risk if skipped:** Events lost when SNS is unavailable, data inconsistency between services
**Status:** In Progress

---

## Problem

Current pattern saves to database then publishes to SNS as two separate operations:

```csharp
// CreateOrderCommandHandler.cs - Current
public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken ct)
{
    var order = Order.Create(...);
    await _repository.SaveAsync(order);              // Step 1: DB succeeds
    await _eventPublisher.PublishAsync(               // Step 2: SNS might fail!
        new OrderCreatedEvent(...));
    return OrderMapper.ToDto(order);
}
```

**Failure scenarios:**

```
Scenario 1: SNS is temporarily down
  DB Save     → ✅ Order created in PostgreSQL
  SNS Publish → ❌ Connection refused
  Result      → Order exists but Payment Service never knows

Scenario 2: Service crashes after DB save
  DB Save     → ✅ Order persisted
  (crash)     → ❌ Service restarts
  SNS Publish → Never happens
  Result      → Orphaned order

Scenario 3: SNS succeeds but DB didn't actually commit
  DB Save     → ❌ Transaction rolls back
  SNS Publish → ✅ Event published
  Result      → Payment Service processes non-existent order
```

## Solution: Transactional Outbox

Save the event to an **outbox_messages** table in the **same database transaction** as the business data. A background publisher polls the outbox and publishes to SNS.

### Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  CreateOrderCommandHandler                                               │
│                                                                          │
│  BEGIN TRANSACTION                                                       │
│  ┌────────────────────────────────────────────────────────────────────┐  │
│  │  1. INSERT INTO orders (...)  VALUES (...)                         │  │
│  │  2. INSERT INTO outbox_messages (event_type, payload, ...) VALUES  │  │
│  └────────────────────────────────────────────────────────────────────┘  │
│  COMMIT  ← Both succeed or both fail                                    │
│                                                                          │
│  No SNS publish here!                                                   │
└──────────────────────────────────────────────────────────────────────────┘

                              ▼

┌──────────────────────────────────────────────────────────────────────────┐
│  OutboxPublisher (BackgroundService)                                     │
│                                                                          │
│  Loop every 5 seconds:                                                   │
│  1. SELECT * FROM outbox_messages WHERE published_at IS NULL             │
│     AND (next_retry_at IS NULL OR next_retry_at <= NOW())               │
│     ORDER BY created_at LIMIT 100                                        │
│                                                                          │
│  2. For each message:                                                    │
│     a. Publish to SNS using stored TopicArn + Payload                   │
│     b. UPDATE SET published_at = NOW() WHERE id = @id                   │
│                                                                          │
│  3. On failure: increment retry_count, set next_retry_at with           │
│     exponential backoff, store error in last_error                      │
│                                                                          │
│  OutboxCleanupService (runs hourly):                                     │
│  - DELETE FROM outbox_messages WHERE published_at < NOW() - 7 days      │
└──────────────────────────────────────────────────────────────────────────┘
```

**Scope**: Order Service + Payment Service (PostgreSQL). DynamoDB services (Catalog, Media) deferred.

---

## Implementation Plan — 7 Pieces

### Piece 1: SharedKernel — OutboxMessage entity + IOutboxWriter interface ✅

**Create:**
- `gearify-shared-kernel/Outbox/OutboxMessage.cs` — Entity with: Id, EventType, TopicArn, Payload (serialized EventEnvelope JSON), MessageAttributes (JSON), CreatedAt, PublishedAt, RetryCount, NextRetryAt, LastError
- `gearify-shared-kernel/Outbox/IOutboxWriter.cs` — Single method: `AddOutboxMessageAsync(OutboxMessage, ct)`

**Delete:**
- `gearify-shared-kernel/Abstractions/IOutboxMessage.cs` — Unused skeleton, replaced by OutboxMessage

### Piece 2: SharedKernel — CreateOutboxMessage helper on SnsEventPublisherBase

**Modify:**
- `gearify-shared-kernel/Events/ISnsEventPublisher.cs` — Add `OutboxMessage CreateOutboxMessage<TEvent>(TEvent)` to interface
- `gearify-shared-kernel/Events/SnsEventPublisherBase.cs` — Implement: resolves TopicArn via existing `GetTopicArn()`, wraps event in `EventEnvelope`, serializes to JSON, stores TopicArn + Payload + MessageAttributes in OutboxMessage

**Key design**: TopicArn is resolved at write-time by the service's publisher, so the OutboxPublisher needs zero routing knowledge.

### Piece 3: Order Service — DbContext + UnitOfWork changes

**Modify:**
- `gearify-order-svc/Infrastructure/Data/OrderDbContext.cs` — Add `DbSet<OutboxMessage>`, configure `outbox_messages` table in `OnModelCreating` with indexes for unpublished polling and cleanup
- `gearify-order-svc/Infrastructure/UnitOfWork/IUnitOfWork.cs` — Extend with `IOutboxWriter`
- `gearify-order-svc/Infrastructure/UnitOfWork/UnitOfWork.cs` — Implement `AddOutboxMessageAsync` via `_context.OutboxMessages.AddAsync()` (same DbContext = same transaction)

### Piece 4: Order Service — Update command handlers

**Modify:**
- `gearify-order-svc/Application/Commands/CreateOrderCommandHandler.cs`
- `gearify-order-svc/Application/Commands/CancelOrderCommandHandler.cs`
- `gearify-order-svc/Application/Commands/ConfirmOrderCommandHandler.cs`

**Pattern for each handler:**
```csharp
// 1. Build event object (same logic as today)
var evt = new OrderCreatedEvent(...);

// 2. Resolve TopicArn + serialize into OutboxMessage
var outbox = _eventPublisher.CreateOutboxMessage(evt);

// 3. Write to same transaction as business data
await unitOfWork.AddOutboxMessageAsync(outbox);

// 4. Atomic commit — both business data + outbox
await unitOfWork.CommitAsync();

// 5. REMOVE the old post-commit _eventPublisher.PublishAsync() call
```

### Piece 5: Payment Service — DbContext + UnitOfWork changes

**Modify:**
- `gearify-payment-svc/Infrastructure/Data/PaymentDbContext.cs` — Add `DbSet<OutboxMessage>` + table config (identical to Order)
- `gearify-payment-svc/Infrastructure/UnitOfWork/IUnitOfWork.cs` — Extend with `IOutboxWriter`
- `gearify-payment-svc/Infrastructure/UnitOfWork/UnitOfWork.cs` — Implement `AddOutboxMessageAsync`

### Piece 6: Payment Service — Update command handlers

**Modify:**
- `gearify-payment-svc/Application/Commands/ProcessOrderPaymentCommandHandler.cs` — Use outbox for PaymentCompleted/PaymentFailed events. Exception path keeps direct SNS publish as best-effort fallback (transaction is rolling back, can't write outbox)
- `gearify-payment-svc/Application/Commands/RefundPaymentCommandHandler.cs` — Use outbox for RefundCompletedEvent

### Piece 7: OutboxPublisher + Registration

**Create:**
- `gearify-shared-kernel/Outbox/OutboxPublisher.cs` — Generic `BackgroundService<TDbContext>` that polls outbox every 5s, publishes to SNS using stored TopicArn/Payload, marks as published, exponential backoff on failure
- `gearify-shared-kernel/Outbox/OutboxPublisherOptions.cs` — Config: PollingInterval, BatchSize, MaxRetries, BackoffBase
- `gearify-shared-kernel/Outbox/OutboxCleanupService.cs` — Deletes published messages older than 7 days (runs hourly)
- `gearify-shared-kernel/Outbox/OutboxExtensions.cs` — `services.AddOutboxPublisher<TDbContext>(options)` helper

**Modify:**
- `gearify-shared-kernel/Gearify.SharedKernel.csproj` — Add `Microsoft.EntityFrameworkCore` package reference
- `gearify-order-svc/Startup.cs` — Add `services.AddOutboxPublisher<OrderDbContext>()`
- `gearify-payment-svc/Startup.cs` — Add `services.AddOutboxPublisher<PaymentDbContext>()`

---

## Affected Services

| Service | Database | Critical Events | Risk if Lost |
|---------|----------|-----------------|--------------|
| **Order Service** | PostgreSQL | `OrderCreatedEvent`, `OrderCancelledEvent`, `OrderConfirmedEvent` | Payment never triggered, order stuck |
| **Payment Service** | PostgreSQL | `PaymentCompletedEvent`, `PaymentFailedEvent`, `RefundCompletedEvent` | Order stuck in Pending |
| Catalog Service | DynamoDB | `ProductCreatedEvent`, `ProductUpdatedEvent` | Search index out of sync (deferred) |
| Media Service | DynamoDB | `MediaUploadedEvent`, `ImageProcessingCompletedEvent` | Thumbnails never generated (deferred) |

---

## Verification

1. `dotnet build` — Solution compiles with no errors
2. Verify the outbox_messages table is created on startup (EnsureCreated)
3. Test flow: Create an order → outbox_messages table gets a row → OutboxPublisher picks it up → publishes to SNS → marks as published
4. Test failure: Stop LocalStack SNS → create order → outbox row stays unpublished → start SNS → OutboxPublisher retries and publishes
5. Verify cleanup: Published messages older than 7 days are removed

---

## Acceptance Criteria

- [ ] Events are saved in the same transaction as business data
- [ ] OutboxPublisher reliably publishes pending messages to SNS
- [ ] Failed publishes are retried with exponential backoff
- [ ] Old published messages are cleaned up automatically
- [ ] If SNS is down, events queue up and publish when SNS recovers
- [ ] No events are lost even if the service crashes after DB commit
