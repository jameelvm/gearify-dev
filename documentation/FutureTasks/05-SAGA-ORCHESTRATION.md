# Task 5: Saga Orchestration Pattern

**Priority:** High
**Effort:** High
**Risk if skipped:** No reliable compensation for multi-service failures, orders stuck in inconsistent states

---

## Problem

The checkout flow spans multiple services that must coordinate:

```
Order Service → Payment Service → Shipping Service → Notification Service
```

Currently, the flow uses **choreography** (event chain): each service publishes an event, and the next service reacts. This works for simple linear flows but becomes problematic as complexity grows.

### Current Choreography Flow

```
1. Order Service  → publishes OrderCreatedEvent
2. Payment Service → receives OrderCreated, processes payment
                   → publishes PaymentCompletedEvent (or PaymentFailedEvent)
3. Order Service  → receives PaymentCompleted, confirms order
4. Shipping Service → (not yet wired) creates shipment
5. Notification Service → receives PaymentFailed, sends email
```

### Problems with Pure Choreography

```
Problem 1: Scattered Compensation Logic
─────────────────────────────────────────
  What if payment succeeds but shipping fails?
  • Who triggers the refund?
  • Who cancels the order?
  • Who notifies the customer?

  With choreography, each service must know about all failure
  scenarios of downstream services — tight coupling through events.

Problem 2: No Single View of Process State
─────────────────────────────────────────
  Q: "What is the current state of order ORD-456?"
  A: Need to check Order Service, Payment Service, AND Shipping Service
     to piece together the full picture.

Problem 3: Adding Steps Requires Multiple Service Changes
─────────────────────────────────────────
  Adding inventory reservation before payment:
  • Inventory Service needs to know about OrderCreated
  • Payment Service needs to wait for InventoryReserved
  • Order Service needs InventoryReservationFailed compensation
  • 3 services modified for 1 new step

Problem 4: Timeout Handling
─────────────────────────────────────────
  If Payment Service never responds:
  • No service is responsible for detecting this
  • Order remains in "Pending" forever
  • Customer has no recourse
```

## Solution: Saga Orchestrator

A central **Saga Orchestrator** (in Order Service) manages the checkout workflow as a state machine. It explicitly defines each step, its compensating action, and timeout behavior.

### Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│  Order Service - CheckoutSaga Orchestrator                               │
│                                                                          │
│  State Machine:                                                          │
│                                                                          │
│  ┌─────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐           │
│  │ Started │───→│ Paying   │───→│ Shipping │───→│Completed │           │
│  └─────────┘    └──────────┘    └──────────┘    └──────────┘           │
│       │              │               │                                   │
│       │         ┌────▼────┐    ┌────▼────┐                              │
│       │         │ Payment │    │ Shipping│                              │
│       │         │ Failed  │    │ Failed  │                              │
│       │         └────┬────┘    └────┬────┘                              │
│       │              │               │                                   │
│       ▼              ▼               ▼                                   │
│  ┌──────────────────────────────────────┐                                │
│  │            Compensating              │                                │
│  │  • Cancel Order                      │                                │
│  │  • Refund Payment (if paid)          │                                │
│  │  • Release Inventory (if reserved)   │                                │
│  │  • Notify Customer                   │                                │
│  └──────────────────────────────────────┘                                │
│                                                                          │
│  Saga State persisted in PostgreSQL (order_sagas table)                  │
│  Each step: Publish command → Wait for event → Transition state          │
└──────────────────────────────────────────────────────────────────────────┘

              │ Commands (SNS)          ▲ Events (SQS)
              ▼                         │

┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Payment    │  │   Shipping   │  │ Notification │
│   Service    │  │   Service    │  │   Service    │
│              │  │              │  │              │
│ Processes    │  │ Creates      │  │ Sends email  │
│ payment and  │  │ shipment and │  │ based on     │
│ publishes    │  │ publishes    │  │ saga events  │
│ result event │  │ result event │  │              │
└──────────────┘  └──────────────┘  └──────────────┘
```

### Implementation Plan

#### Step 1: Define Saga States

```csharp
// gearify-order-svc/Domain/Enums/CheckoutSagaState.cs
public enum CheckoutSagaState
{
    // Forward flow
    NotStarted,
    OrderCreated,
    PaymentPending,
    PaymentCompleted,
    ShipmentPending,
    ShipmentCreated,
    Completed,

    // Compensation flow
    PaymentFailed,
    ShipmentFailed,
    Compensating,
    Compensated,

    // Terminal failure
    Failed
}
```

#### Step 2: Create Saga Entity

```csharp
// gearify-order-svc/Domain/Entities/CheckoutSaga.cs
public class CheckoutSaga
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public CheckoutSagaState State { get; set; } = CheckoutSagaState.NotStarted;

    // Tracking
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? TimeoutAt { get; set; }

    // Step results (for compensation)
    public string? PaymentId { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? ShipmentId { get; set; }

    // Error tracking
    public string? FailureReason { get; set; }
    public int CompensationRetryCount { get; set; }

    // State history
    public List<SagaStateTransition> StateHistory { get; set; } = new();
}

public class SagaStateTransition
{
    public CheckoutSagaState FromState { get; set; }
    public CheckoutSagaState ToState { get; set; }
    public DateTime TransitionedAt { get; set; }
    public string? Reason { get; set; }
}
```

#### Step 3: Create PostgreSQL Table

```sql
-- Add to gearify_orders database
CREATE TABLE checkout_sagas (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id                UUID NOT NULL REFERENCES orders(id),
    user_id                 VARCHAR(255) NOT NULL,
    tenant_id               VARCHAR(255) NOT NULL,
    state                   VARCHAR(50) NOT NULL DEFAULT 'NotStarted',
    created_at              TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at              TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at            TIMESTAMP NULL,
    timeout_at              TIMESTAMP NULL,
    payment_id              VARCHAR(255) NULL,
    payment_intent_id       VARCHAR(255) NULL,
    shipment_id             VARCHAR(255) NULL,
    failure_reason          TEXT NULL,
    compensation_retry_count INT NOT NULL DEFAULT 0,
    state_history           JSONB NOT NULL DEFAULT '[]'
);

CREATE INDEX idx_saga_state ON checkout_sagas(state) WHERE state NOT IN ('Completed', 'Compensated', 'Failed');
CREATE INDEX idx_saga_timeout ON checkout_sagas(timeout_at) WHERE timeout_at IS NOT NULL AND completed_at IS NULL;
CREATE INDEX idx_saga_order ON checkout_sagas(order_id);
```

#### Step 4: Implement Saga Orchestrator

```csharp
// gearify-order-svc/Application/Sagas/CheckoutSagaOrchestrator.cs
public class CheckoutSagaOrchestrator
{
    private readonly ICheckoutSagaRepository _sagaRepository;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<CheckoutSagaOrchestrator> _logger;

    // ── Forward Steps ───────────────────────────────────────

    public async Task StartAsync(Guid orderId, string userId, string tenantId)
    {
        var saga = new CheckoutSaga
        {
            OrderId = orderId,
            UserId = userId,
            TenantId = tenantId,
            State = CheckoutSagaState.OrderCreated,
            TimeoutAt = DateTime.UtcNow.AddMinutes(30)  // Saga timeout
        };

        await _sagaRepository.SaveAsync(saga);

        // Transition: OrderCreated → PaymentPending
        await TransitionAsync(saga, CheckoutSagaState.PaymentPending);

        // Command: Request payment processing
        await _eventPublisher.PublishAsync(
            new OrderCreatedEvent(orderId, userId, tenantId, ...),
            tenantId);
    }

    public async Task HandlePaymentCompletedAsync(Guid orderId, string paymentId)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId);
        if (saga.State != CheckoutSagaState.PaymentPending) return;

        saga.PaymentId = paymentId;
        await TransitionAsync(saga, CheckoutSagaState.PaymentCompleted);

        // Transition: PaymentCompleted → ShipmentPending
        await TransitionAsync(saga, CheckoutSagaState.ShipmentPending);

        // Command: Request shipment creation
        await _eventPublisher.PublishAsync(
            new OrderConfirmedEvent(orderId, paymentId, ...),
            saga.TenantId);
    }

    public async Task HandleShipmentCreatedAsync(Guid orderId, string shipmentId)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId);
        if (saga.State != CheckoutSagaState.ShipmentPending) return;

        saga.ShipmentId = shipmentId;
        await TransitionAsync(saga, CheckoutSagaState.ShipmentCreated);
        await TransitionAsync(saga, CheckoutSagaState.Completed);

        saga.CompletedAt = DateTime.UtcNow;
        await _sagaRepository.SaveAsync(saga);
    }

    // ── Compensation Steps ──────────────────────────────────

    public async Task HandlePaymentFailedAsync(Guid orderId, string reason)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId);
        if (saga.State != CheckoutSagaState.PaymentPending) return;

        saga.FailureReason = reason;
        await TransitionAsync(saga, CheckoutSagaState.PaymentFailed);
        await CompensateAsync(saga);
    }

    public async Task HandleShipmentFailedAsync(Guid orderId, string reason)
    {
        var saga = await _sagaRepository.GetByOrderIdAsync(orderId);
        if (saga.State != CheckoutSagaState.ShipmentPending) return;

        saga.FailureReason = reason;
        await TransitionAsync(saga, CheckoutSagaState.ShipmentFailed);
        await CompensateAsync(saga);
    }

    private async Task CompensateAsync(CheckoutSaga saga)
    {
        await TransitionAsync(saga, CheckoutSagaState.Compensating);

        // Reverse order compensation:
        // 1. Refund payment (if completed)
        if (saga.PaymentId != null)
        {
            await _eventPublisher.PublishAsync(
                new RefundInitiatedEvent(saga.OrderId, saga.PaymentId, ...),
                saga.TenantId);
        }

        // 2. Cancel order
        await _eventPublisher.PublishAsync(
            new OrderCancelledEvent(saga.OrderId, saga.FailureReason, ...),
            saga.TenantId);

        // 3. Notify customer
        // (Notification Service subscribes to OrderCancelled / PaymentFailed)

        await TransitionAsync(saga, CheckoutSagaState.Compensated);
        saga.CompletedAt = DateTime.UtcNow;
        await _sagaRepository.SaveAsync(saga);
    }

    // ── State Management ────────────────────────────────────

    private async Task TransitionAsync(CheckoutSaga saga, CheckoutSagaState newState)
    {
        var oldState = saga.State;
        saga.State = newState;
        saga.UpdatedAt = DateTime.UtcNow;
        saga.StateHistory.Add(new SagaStateTransition
        {
            FromState = oldState,
            ToState = newState,
            TransitionedAt = DateTime.UtcNow
        });

        _logger.LogInformation(
            "Saga {SagaId} for Order {OrderId}: {OldState} → {NewState}",
            saga.Id, saga.OrderId, oldState, newState);

        await _sagaRepository.SaveAsync(saga);
    }
}
```

#### Step 5: Add Timeout Monitor

```csharp
// gearify-order-svc/Application/Sagas/SagaTimeoutMonitor.cs
public class SagaTimeoutMonitor : BackgroundService
{
    // Runs every 60 seconds
    // Finds sagas where:
    //   - State is NOT terminal (Completed, Compensated, Failed)
    //   - TimeoutAt < NOW()
    // For each timed-out saga:
    //   - Mark as Failed with reason "Saga timed out"
    //   - Trigger compensation
    //   - Log warning

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var timedOut = await _sagaRepository.GetTimedOutSagasAsync(ct);

            foreach (var saga in timedOut)
            {
                _logger.LogWarning(
                    "Saga {SagaId} for Order {OrderId} timed out in state {State}",
                    saga.Id, saga.OrderId, saga.State);

                saga.FailureReason = $"Saga timed out in state {saga.State}";
                await _orchestrator.CompensateAsync(saga);
            }

            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }
}
```

#### Step 6: Wire Into Existing Event Handlers

```csharp
// gearify-order-svc/Application/EventHandlers/PaymentEventHandler.cs
public class PaymentEventHandler : IEventHandler<PaymentEventMessage>
{
    private readonly CheckoutSagaOrchestrator _saga;

    public async Task<bool> HandleAsync(PaymentEventMessage evt, CancellationToken ct)
    {
        return evt.EventType switch
        {
            "PaymentCompleted" => await HandlePaymentCompleted(evt, ct),
            "PaymentFailed" => await HandlePaymentFailed(evt, ct),
            _ => true
        };
    }

    private async Task<bool> HandlePaymentCompleted(PaymentEventMessage evt, CancellationToken ct)
    {
        // Delegate to saga orchestrator instead of direct order update
        await _saga.HandlePaymentCompletedAsync(
            Guid.Parse(evt.OrderId),
            evt.PaymentId);
        return true;
    }

    private async Task<bool> HandlePaymentFailed(PaymentEventMessage evt, CancellationToken ct)
    {
        await _saga.HandlePaymentFailedAsync(
            Guid.Parse(evt.OrderId),
            evt.FailureReason);
        return true;
    }
}
```

### State Machine Diagram

```
                    ┌───────────┐
                    │ NotStarted│
                    └─────┬─────┘
                          │ CreateOrder
                          ▼
                    ┌───────────┐
                    │  Order    │
                    │  Created  │
                    └─────┬─────┘
                          │ RequestPayment
                          ▼
                    ┌───────────┐
              ┌─────│  Payment  │─────┐
              │     │  Pending  │     │
              │     └───────────┘     │
              │ PaymentCompleted      │ PaymentFailed
              ▼                       ▼
        ┌───────────┐          ┌───────────┐
        │  Payment  │          │  Payment  │
        │ Completed │          │  Failed   │
        └─────┬─────┘          └─────┬─────┘
              │ RequestShipment      │
              ▼                       │
        ┌───────────┐                │
  ┌─────│  Shipment │─────┐          │
  │     │  Pending  │     │          │
  │     └───────────┘     │          │
  │ ShipmentCreated       │ ShipmentFailed
  ▼                       ▼          │
┌───────────┐       ┌───────────┐    │
│  Shipment │       │  Shipment │    │
│  Created  │       │  Failed   │    │
└─────┬─────┘       └─────┬─────┘    │
      │                    │          │
      ▼                    ▼          ▼
┌───────────┐       ┌─────────────────────┐
│ Completed │       │    Compensating     │
└───────────┘       │  • Refund payment   │
                    │  • Cancel order     │
                    │  • Notify customer  │
                    └──────────┬──────────┘
                               │
                               ▼
                    ┌───────────────────┐
                    │    Compensated    │
                    └───────────────────┘
```

### Saga vs Choreography Comparison

| Aspect | Current (Choreography) | Saga Orchestrator |
|--------|----------------------|-------------------|
| **Flow visibility** | Scattered across services | Single `CheckoutSaga` entity |
| **Adding new step** | Modify multiple services | Add step to orchestrator |
| **Compensation** | Each service handles own rollback | Centralized compensation logic |
| **Timeout handling** | None | `SagaTimeoutMonitor` |
| **Debugging** | Grep logs across services | Query `checkout_sagas` table |
| **Complexity** | Simple for 2-3 steps | Better for 4+ steps |
| **Coupling** | Services coupled via events | Orchestrator coupled to all steps |

### Alternative: MassTransit Saga

MassTransit provides built-in saga support with EF Core persistence:

```csharp
// MassTransit approach
public class CheckoutStateMachine : MassTransitStateMachine<CheckoutSagaState>
{
    public CheckoutStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, x => x.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentCompleted, x => x.CorrelateById(c => c.Message.OrderId));
        Event(() => PaymentFailed, x => x.CorrelateById(c => c.Message.OrderId));

        Initially(
            When(OrderCreated)
                .Then(ctx => ctx.Saga.OrderId = ctx.Message.OrderId)
                .TransitionTo(PaymentPending)
                .Publish(ctx => new ProcessPaymentCommand(ctx.Saga.OrderId)));

        During(PaymentPending,
            When(PaymentCompleted)
                .TransitionTo(ShipmentPending)
                .Publish(ctx => new CreateShipmentCommand(ctx.Saga.OrderId)),
            When(PaymentFailed)
                .TransitionTo(Compensating)
                .Publish(ctx => new CancelOrderCommand(ctx.Saga.OrderId)));

        // ... additional states
    }
}
```

### Affected Services

| Service | Changes |
|---------|---------|
| **Order Service** | New: `CheckoutSagaOrchestrator`, `SagaTimeoutMonitor`, `CheckoutSaga` entity, saga repository, DB migration |
| **Payment Service** | Minor: Ensure `PaymentCompletedEvent` and `PaymentFailedEvent` include all fields needed by saga |
| **Shipping Service** | Minor: Publish `ShipmentCreatedEvent` / `ShipmentFailedEvent` for saga |
| **Notification Service** | No changes: continues to subscribe to events |

### Files to Create/Modify

| Action | File |
|--------|------|
| Create | `gearify-order-svc/Domain/Enums/CheckoutSagaState.cs` |
| Create | `gearify-order-svc/Domain/Entities/CheckoutSaga.cs` |
| Create | `gearify-order-svc/Application/Sagas/CheckoutSagaOrchestrator.cs` |
| Create | `gearify-order-svc/Application/Sagas/SagaTimeoutMonitor.cs` |
| Create | `gearify-order-svc/Infrastructure/Repositories/CheckoutSagaRepository.cs` |
| Create | `gearify-order-svc/Infrastructure/Data/CheckoutSagaConfiguration.cs` (EF Core) |
| Modify | `gearify-order-svc/Infrastructure/Data/OrderDbContext.cs` (add `CheckoutSagas` DbSet) |
| Modify | `gearify-order-svc/Application/EventHandlers/PaymentEventHandler.cs` (delegate to saga) |
| Modify | `gearify-order-svc/Application/Commands/CreateOrderCommandHandler.cs` (start saga) |
| Modify | `gearify-order-svc/Startup.cs` (register saga services) |
| Modify | `gearify-umbrella/postgres/init-databases.sql` (add `checkout_sagas` table) |

### Acceptance Criteria

- [ ] `CheckoutSaga` tracks full lifecycle of a checkout flow
- [ ] Saga state transitions are persisted in PostgreSQL
- [ ] Forward flow: Order → Payment → Shipping → Completed
- [ ] Payment failure triggers compensation (cancel order, notify customer)
- [ ] Shipping failure triggers compensation (refund payment, cancel order, notify customer)
- [ ] Saga timeout (30 min) triggers compensation automatically
- [ ] `checkout_sagas` table provides full audit trail of state transitions
- [ ] Saga state is queryable via Order Service API (e.g., `GET /api/orders/{id}/saga-status`)
- [ ] All compensation actions are idempotent (safe to retry)

### Recommended Implementation Order

1. **Start with Task 1 (Idempotent Consumer)** — needed for reliable saga event handling
2. **Then Task 2 (Transactional Outbox)** — saga state + event must be atomic
3. **Then Task 3 (Correlation ID)** — trace saga flow across services
4. **Then Task 5 (this task)** — implement the saga orchestrator
5. **Finally Task 4 (AsyncAPI)** — document the final event architecture
