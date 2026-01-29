# Cancel Order & Refund - Implementation Plan

## Overview

This document breaks down the implementation into small, manageable tasks. Each task is designed to be:
- Self-contained and testable
- Reviewable before moving to the next task
- Buildable incrementally

**Estimated Total Tasks:** 25 tasks across 6 phases

---

## Phase 1: Infrastructure Setup

### Task 1.1: Create New SQS Queues

**File:** `gearify-umbrella/localstack/init-aws.sh`

**Changes:**
- Add `gearify-order-refund-queue` (Payment Service consumes OrderCancelledEvent)
- Add `gearify-notification-refund-queue` (Notification Service consumes RefundCompleted/Failed)

**Verification:**
```bash
# After docker-compose restart
awslocal sqs list-queues --region us-east-1 | grep -E "(order-refund|notification-refund)"
```

---

### Task 1.2: Create SNS Subscriptions

**File:** `gearify-umbrella/localstack/init-aws.sh`

**Changes:**
- Subscribe `gearify-order-refund-queue` to `gearify-order-events` topic (filter: `OrderCancelledEvent`)
- Subscribe `gearify-notification-refund-queue` to `gearify-payment-events` topic (filter: `RefundCompletedEvent`, `RefundFailedEvent`)

**Verification:**
```bash
awslocal sns list-subscriptions-by-topic --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-order-events
awslocal sns list-subscriptions-by-topic --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-payment-events
```

---

### Task 1.3: Update Service Configurations

**Files:**
- `gearify-payment-svc/appsettings.Development.json` - Add `OrderRefundQueueUrl`
- `gearify-notification-svc/appsettings.Development.json` - Add `RefundEventsQueueUrl`

**Changes:**
```json
// Payment Service
"MessagingConfiguration": {
  "SQS": {
    "OrderRefundQueueUrl": "http://localstack:4566/000000000000/gearify-order-refund-queue"
  }
}

// Notification Service
"MessagingConfiguration": {
  "SQS": {
    "RefundEventsQueueUrl": "http://localstack:4566/000000000000/gearify-notification-refund-queue"
  }
}
```

---

## Phase 2: Order Service - Events & Models

### Task 2.1: Create OrderCancelledEvent

**File:** `gearify-order-svc/Domain/Events/OrderCancelledEvent.cs` (NEW)

**Create:**
```csharp
public record OrderCancelledEvent : IDomainEvent
{
    public string TenantId { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; }
    public string UserId { get; init; }
    public string Reason { get; init; }
    public string? CancelledBy { get; init; }
    public Guid? PaymentId { get; init; }
    public decimal? PaidAmount { get; init; }
    public string? Currency { get; init; }
    public DateTime OccurredAt { get; init; }
}
```

---

### Task 2.2: Update Order Entity (if needed)

**File:** `gearify-order-svc/Domain/Entities/Order.cs`

**Check if these fields exist, add if missing:**
```csharp
public string? CancellationReason { get; set; }
public string? CancellationRequestedBy { get; set; }
public DateTime? CancellationRequestedAt { get; set; }
```

---

### Task 2.3: Update SnsEventPublisher to Route OrderCancelledEvent

**File:** `gearify-order-svc/Infrastructure/Messaging/SnsEventPublisher.cs`

**Changes:**
- Add `OrderCancelledEvent` to `GetTopicArn()` method routing

```csharp
protected override string? GetTopicArn(string eventType)
{
    return eventType switch
    {
        nameof(OrderCreatedEvent) => _settings.SNS.OrderEventsTopicArn,
        nameof(OrderCancelledEvent) => _settings.SNS.OrderEventsTopicArn,  // ADD THIS
        nameof(OrderConfirmedEvent) => _settings.SNS.OrderEventsTopicArn,
        _ => _settings.SNS.OrderEventsTopicArn
    };
}
```

---

## Phase 3: Order Service - Cancel Command Handler

### Task 3.1: Create CancelOrderResult Model

**File:** `gearify-order-svc/Application/Commands/CancelOrderResult.cs` (check if exists, update if needed)

**Ensure it has:**
```csharp
public record CancelOrderResult
{
    public bool Success { get; init; }
    public bool IsPending { get; init; }  // For deferred cancellation
    public Guid? OrderId { get; init; }
    public string? Message { get; init; }
    public bool RefundInitiated { get; init; }

    public static CancelOrderResult Succeeded(Guid orderId, bool refundInitiated, string message) => ...
    public static CancelOrderResult Pending(Guid orderId, string message) => ...
    public static CancelOrderResult Failed(string message) => ...
}
```

---

### Task 3.2: Update CancelOrderCommandHandler - Basic Flow

**File:** `gearify-order-svc/Application/Commands/CancelOrderCommandHandler.cs`

**Changes (Part 1 - without race condition handling):**
1. After setting `OrderStatus = Cancelled`, publish `OrderCancelledEvent`
2. Include `PaymentId` if order was in `Paid` or `Processing` status

```csharp
// After order.Status = OrderStatus.Cancelled;
var hadPayment = previousStatus == OrderStatus.Paid ||
                 previousStatus == OrderStatus.Processing;

var evt = new OrderCancelledEvent
{
    TenantId = order.TenantId,
    OrderId = order.Id,
    OrderNumber = order.OrderNumber,
    UserId = order.UserId,
    Reason = request.Reason,
    CancelledBy = request.CancelledBy,
    PaymentId = hadPayment ? order.PaymentId : null,
    PaidAmount = hadPayment ? order.TotalAmount : null,
    Currency = order.Currency,
    OccurredAt = DateTime.UtcNow
};

await _eventPublisher.PublishAsync(evt, cancellationToken);
```

---

### Task 3.3: Update CancelOrderCommandHandler - Race Condition Handling

**File:** `gearify-order-svc/Application/Commands/CancelOrderCommandHandler.cs`

**Changes (Part 2 - add deferred cancellation):**

```csharp
if (order.Status == OrderStatus.PaymentProcessing)
{
    // Don't cancel immediately - mark for deferred cancellation
    order.SagaState = SagaState.Compensating;
    order.CancellationReason = request.Reason;
    order.CancellationRequestedBy = request.CancelledBy;
    order.CancellationRequestedAt = DateTime.UtcNow;

    await _orderRepository.UpdateAsync(order, cancellationToken);

    return CancelOrderResult.Pending(
        order.Id,
        "Cancellation requested. Payment is being processed. If payment succeeds, we will automatically refund it."
    );
}
```

---

### Task 3.4: Update PaymentEventHandler - Check Deferred Cancellation

**File:** `gearify-order-svc/Infrastructure/Messaging/PaymentEventHandler.cs`

**Changes:**
- In `HandlePaymentCompletedAsync`: Check if `SagaState == Compensating`
  - If yes: Cancel order and publish `OrderCancelledEvent` with PaymentId
  - If no: Normal flow (update to Paid)

- In `HandlePaymentFailedAsync`: Check if `SagaState == Compensating`
  - If yes: Cancel order and publish `OrderCancelledEvent` without PaymentId
  - If no: Normal flow (update to PaymentFailed)

---

### Task 3.5: Update PaymentEventHandler - Handle RefundCompletedEvent

**File:** `gearify-order-svc/Infrastructure/Messaging/PaymentEventHandler.cs`

**Changes:**
- Add new case in switch statement for `RefundCompletedEvent`
- Update order status to `Refunded`
- Update `SagaState` to `Completed`

```csharp
"RefundCompletedEvent" => await HandleRefundCompletedAsync(message, ct),

private async Task<bool> HandleRefundCompletedAsync(PaymentEventMessage message, CancellationToken ct)
{
    var order = await GetOrderAsync(message.OrderId, ct);
    if (order == null) return true;

    order.Status = OrderStatus.Refunded;
    order.SagaState = SagaState.Completed;
    order.UpdatedAt = DateTime.UtcNow;

    await _orderRepository.UpdateAsync(order, ct);

    _logger.LogInformation("Order {OrderId} marked as Refunded", order.Id);
    return true;
}
```

---

### Task 3.6: Update PaymentEventMessage Model

**File:** `gearify-order-svc/Infrastructure/Messaging/PaymentEventMessage.cs`

**Add fields for refund events:**
```csharp
public Guid? RefundId { get; init; }
public decimal? RefundAmount { get; init; }
public string? RefundReason { get; init; }
```

---

## Phase 4: Payment Service - Refund Flow

### Task 4.1: Create OrderCancelledEventMessage

**File:** `gearify-payment-svc/Infrastructure/Messaging/Events/Inbound/OrderCancelledEventMessage.cs` (NEW)

```csharp
public record OrderCancelledEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? CancelledBy { get; init; }
    public Guid? PaymentId { get; init; }
    public decimal? PaidAmount { get; init; }
    public string? Currency { get; init; }
    public DateTime OccurredAt { get; init; }
}
```

---

### Task 4.2: Create OrderCancelledEventHandler

**File:** `gearify-payment-svc/Infrastructure/Messaging/OrderCancelledEventHandler.cs` (NEW)

```csharp
public class OrderCancelledEventHandler : IEventHandler<OrderCancelledEventMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderCancelledEventHandler> _logger;

    public async Task<bool> HandleAsync(OrderCancelledEventMessage message, CancellationToken ct)
    {
        // Only process if there's a payment to refund
        if (message.PaymentId == null || message.PaidAmount == null)
        {
            _logger.LogInformation(
                "Order {OrderId} cancelled without payment. No refund needed.",
                message.OrderId);
            return true;
        }

        _logger.LogInformation(
            "Processing refund for cancelled order {OrderId}. Amount: {Amount} {Currency}",
            message.OrderId, message.PaidAmount, message.Currency);

        var command = new RefundPaymentCommand(
            TransactionId: message.PaymentId.Value,
            Amount: message.PaidAmount.Value,
            Reason: $"Order cancelled: {message.Reason}"
        );

        var result = await _mediator.Send(command, ct);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Refund failed for order {OrderId}: {Error}. Will retry.",
                message.OrderId, result.ErrorMessage);
            return false; // Keep in queue for retry
        }

        return true;
    }
}
```

---

### Task 4.3: Update RefundPaymentCommandHandler - Publish Events

**File:** `gearify-payment-svc/Application/Commands/RefundPaymentCommandHandler.cs`

**Changes:**
- After successful refund: Publish `RefundCompletedEvent`
- After failed refund: Publish `RefundFailedEvent` (only on final failure from DLQ)

```csharp
// On success:
var evt = new RefundCompletedEvent
{
    TenantId = transaction.TenantId,
    RefundId = refund.Id,
    TransactionId = transaction.Id,
    OrderId = transaction.OrderId,
    OrderNumber = transaction.OrderNumber ?? "",
    UserId = transaction.UserId,
    RefundAmount = refund.Amount,
    OriginalAmount = transaction.Amount,
    Currency = transaction.Currency,
    Reason = request.Reason,
    ProviderRefundId = refund.ProviderRefundId,
    OccurredAt = DateTime.UtcNow
};

await _eventPublisher.PublishAsync(evt, cancellationToken);
```

---

### Task 4.4: Create/Update RefundCompletedEvent

**File:** `gearify-payment-svc/Events/RefundCompletedEvent.cs` (check if exists)

```csharp
public record RefundCompletedEvent : IDomainEvent
{
    public string TenantId { get; init; } = string.Empty;
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public decimal OriginalAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? ProviderRefundId { get; init; }
    public DateTime OccurredAt { get; init; }
}
```

---

### Task 4.5: Create RefundFailedEvent

**File:** `gearify-payment-svc/Events/RefundFailedEvent.cs` (NEW)

```csharp
public record RefundFailedEvent : IDomainEvent
{
    public string TenantId { get; init; } = string.Empty;
    public Guid TransactionId { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public DateTime OccurredAt { get; init; }
}
```

---

### Task 4.6: Update SnsEventPublisher (Payment Service)

**File:** `gearify-payment-svc/Infrastructure/Messaging/SnsEventPublisher.cs`

**Changes:**
- Add `RefundCompletedEvent` and `RefundFailedEvent` to routing

```csharp
protected override string? GetTopicArn(string eventType)
{
    return eventType switch
    {
        nameof(PaymentCompletedEvent) => _settings.SNS.PaymentEventsTopicArn,
        nameof(PaymentFailedEvent) => _settings.SNS.PaymentEventsTopicArn,
        nameof(RefundCompletedEvent) => _settings.SNS.PaymentEventsTopicArn,  // ADD
        nameof(RefundFailedEvent) => _settings.SNS.PaymentEventsTopicArn,     // ADD
        _ => _settings.SNS.PaymentEventsTopicArn
    };
}
```

---

### Task 4.7: Register OrderCancelledEvent Consumer in Startup

**File:** `gearify-payment-svc/Startup.cs`

**Changes:**
- Add `IEventQueue<OrderCancelledEventMessage>` registration
- Add `IEventHandler<OrderCancelledEventMessage>` registration
- Add `EventQueueProcessor<OrderCancelledEventMessage>` hosted service

---

## Phase 5: Notification Service - Email Notifications

### Task 5.1: Create OrderCancelled.html Template

**File:** `gearify-notification-svc/Infrastructure/EmailTemplates/OrderCancelled.html` (NEW)

**Placeholders:** `{{FirstName}}`, `{{OrderNumber}}`, `{{CancellationReason}}`, `{{OrderLink}}`

**Subject:** "Your Gearify Order Has Been Cancelled"

---

### Task 5.2: Create OrderCancelledRefunded.html Template

**File:** `gearify-notification-svc/Infrastructure/EmailTemplates/OrderCancelledRefunded.html` (NEW)

**Placeholders:** `{{FirstName}}`, `{{OrderNumber}}`, `{{RefundAmount}}`, `{{Currency}}`, `{{CancellationReason}}`, `{{RefundId}}`, `{{OrderLink}}`

**Subject:** "Your Gearify Order Has Been Cancelled and Refunded"

---

### Task 5.3: Create RefundFailed.html Template

**File:** `gearify-notification-svc/Infrastructure/EmailTemplates/RefundFailed.html` (NEW)

**Placeholders:** `{{FirstName}}`, `{{OrderNumber}}`, `{{Amount}}`, `{{Currency}}`, `{{ErrorMessage}}`, `{{SupportLink}}`

**Subject:** "Issue Processing Your Refund - Action Required"

---

### Task 5.4: Update EmailTemplateService Subject Mappings

**File:** `gearify-notification-svc/Infrastructure/Email/EmailTemplateService.cs`

**Changes:**
```csharp
private static readonly Dictionary<string, string> SubjectMappings = new()
{
    // ... existing mappings ...
    ["OrderCancelled"] = "Your Gearify Order Has Been Cancelled",
    ["OrderCancelledRefunded"] = "Your Gearify Order Has Been Cancelled and Refunded",
    ["RefundFailed"] = "Issue Processing Your Refund - Action Required"
};
```

---

### Task 5.5: Create RefundEventMessage

**File:** `gearify-notification-svc/Infrastructure/Messaging/RefundEventMessage.cs` (NEW)

```csharp
public record RefundEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public Guid? RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public decimal OriginalAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? ProviderRefundId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime OccurredAt { get; init; }
}
```

---

### Task 5.6: Create RefundEventHandler

**File:** `gearify-notification-svc/Infrastructure/Messaging/RefundEventHandler.cs` (NEW)

```csharp
public class RefundEventHandler : IEventHandler<RefundEventMessage>
{
    public async Task<bool> HandleAsync(RefundEventMessage message, CancellationToken ct)
    {
        return message.EventType switch
        {
            "RefundCompletedEvent" => await HandleRefundCompletedAsync(message, ct),
            "RefundFailedEvent" => await HandleRefundFailedAsync(message, ct),
            _ => true
        };
    }

    private async Task<bool> HandleRefundCompletedAsync(RefundEventMessage message, CancellationToken ct)
    {
        // 1. Fetch user from Auth Service
        // 2. Render OrderCancelledRefunded template
        // 3. Send email
    }

    private async Task<bool> HandleRefundFailedAsync(RefundEventMessage message, CancellationToken ct)
    {
        // 1. Fetch user from Auth Service
        // 2. Render RefundFailed template
        // 3. Send email to customer
        // 4. Alert admin (email or logging)
    }
}
```

---

### Task 5.7: Register RefundEventHandler in Startup

**File:** `gearify-notification-svc/Startup.cs`

**Changes:**
- Add `IEventQueue<RefundEventMessage>` registration
- Add `IEventHandler<RefundEventMessage>` registration
- Add `EventQueueProcessor<RefundEventMessage>` hosted service

---

## Phase 6: Testing & Validation

### Task 6.1: Manual Test - Cancel Unpaid Order

**Steps:**
1. Create an order (don't complete payment)
2. Call `POST /api/orders/{id}/cancel`
3. Verify:
   - Order status = Cancelled
   - `OrderCancelledEvent` published (PaymentId = null)
   - Email `OrderCancelled.html` sent

---

### Task 6.2: Manual Test - Cancel Paid Order

**Steps:**
1. Create an order and complete payment
2. Call `POST /api/orders/{id}/cancel`
3. Verify:
   - Order status = Cancelled
   - `OrderCancelledEvent` published (with PaymentId)
   - Refund processed via Stripe
   - `RefundCompletedEvent` published
   - Order status updated to Refunded
   - Email `OrderCancelledRefunded.html` sent

---

### Task 6.3: Manual Test - Race Condition

**Steps:**
1. Create an order
2. Trigger payment processing (but delay the response using mock)
3. While payment is processing, call cancel
4. Verify:
   - Order `SagaState = Compensating`
   - Response says "Cancellation pending"
5. Let payment complete
6. Verify:
   - Order status = Cancelled
   - Refund automatically triggered
   - Email sent after refund completes

---

## Task Execution Order

```
Phase 1: Infrastructure (Tasks 1.1 → 1.3)
    │
    ▼
Phase 2: Order Service Events (Tasks 2.1 → 2.3)
    │
    ▼
Phase 3: Order Service Handler (Tasks 3.1 → 3.6)
    │
    ▼
Phase 4: Payment Service (Tasks 4.1 → 4.7)
    │
    ▼
Phase 5: Notification Service (Tasks 5.1 → 5.7)
    │
    ▼
Phase 6: Testing (Tasks 6.1 → 6.3)
```

---

## Quick Reference: Files to Create/Modify

### New Files (10)

| Service | File |
|---------|------|
| Order | `Domain/Events/OrderCancelledEvent.cs` |
| Payment | `Infrastructure/Messaging/Events/Inbound/OrderCancelledEventMessage.cs` |
| Payment | `Infrastructure/Messaging/OrderCancelledEventHandler.cs` |
| Payment | `Events/RefundFailedEvent.cs` |
| Notification | `Infrastructure/Messaging/RefundEventMessage.cs` |
| Notification | `Infrastructure/Messaging/RefundEventHandler.cs` |
| Notification | `Infrastructure/EmailTemplates/OrderCancelled.html` |
| Notification | `Infrastructure/EmailTemplates/OrderCancelledRefunded.html` |
| Notification | `Infrastructure/EmailTemplates/RefundFailed.html` |

### Modified Files (12)

| Service | File |
|---------|------|
| Infrastructure | `gearify-umbrella/localstack/init-aws.sh` |
| Order | `Domain/Entities/Order.cs` (if fields missing) |
| Order | `Infrastructure/Messaging/SnsEventPublisher.cs` |
| Order | `Application/Commands/CancelOrderCommandHandler.cs` |
| Order | `Infrastructure/Messaging/PaymentEventHandler.cs` |
| Order | `Infrastructure/Messaging/PaymentEventMessage.cs` |
| Payment | `Application/Commands/RefundPaymentCommandHandler.cs` |
| Payment | `Events/RefundCompletedEvent.cs` (if exists) |
| Payment | `Infrastructure/Messaging/SnsEventPublisher.cs` |
| Payment | `Startup.cs` |
| Notification | `Infrastructure/Email/EmailTemplateService.cs` |
| Notification | `Startup.cs` |

---

## Ready to Start?

Let me know when you want to begin with **Task 1.1: Create New SQS Queues**, and we'll work through each task step by step.
