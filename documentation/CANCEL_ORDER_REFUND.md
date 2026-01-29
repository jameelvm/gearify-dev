# Cancel Order & Refund Payment

## Overview

This document describes the event-driven architecture for handling order cancellations and automatic payment refunds in the Gearify platform. The implementation follows the existing SNS/SQS fanout pattern and addresses critical race conditions that can occur when cancellation requests overlap with payment processing.

---

## Table of Contents

1. [Business Requirements](#business-requirements)
2. [Architecture Overview](#architecture-overview)
3. [Race Condition Handling](#race-condition-handling)
4. [Event Flow Diagrams](#event-flow-diagrams)
5. [Event Definitions](#event-definitions)
6. [Service Responsibilities](#service-responsibilities)
7. [Infrastructure Setup](#infrastructure-setup)
8. [Email Notifications](#email-notifications)
9. [Error Handling & Retry Strategy](#error-handling--retry-strategy)
10. [API Reference](#api-reference)
11. [Implementation Checklist](#implementation-checklist)

---

## Business Requirements

### Functional Requirements

1. **Order Cancellation**: Users can cancel orders in eligible statuses
2. **Automatic Refund**: When a paid order is cancelled, automatically process a full refund
3. **Email Notifications**: Send appropriate email notifications based on the outcome
4. **Race Condition Safety**: Handle cancellation requests during active payment processing

### Cancellable Order Statuses

| Status | Can Cancel? | Refund Required? |
|--------|-------------|------------------|
| `Pending` | Yes | No |
| `PaymentProcessing` | Yes (deferred) | Maybe (depends on payment outcome) |
| `PaymentFailed` | Yes | No |
| `Paid` | Yes | Yes |
| `Processing` | Yes | Yes |
| `Shipped` | No | N/A |
| `Delivered` | No | N/A |
| `Cancelled` | No | N/A |
| `Refunded` | No | N/A |

### Email Notification Matrix

| Scenario | Email Template | Trigger Event |
|----------|----------------|---------------|
| Order cancelled (no payment) | `OrderCancelled.html` | `OrderCancelledEvent` with no PaymentId |
| Order cancelled + refund processed | `OrderCancelledRefunded.html` | `RefundCompletedEvent` |
| Refund failed (after retries) | `RefundFailed.html` | `RefundFailedEvent` (from DLQ) |

---

## Architecture Overview

### High-Level Flow

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                           CANCEL ORDER & REFUND ARCHITECTURE                          │
│                                                                                       │
│                                                                                       │
│   ┌─────────────┐                                                                     │
│   │   CLIENT    │                                                                     │
│   │  (Web/App)  │                                                                     │
│   └──────┬──────┘                                                                     │
│          │                                                                            │
│          │ POST /api/orders/{id}/cancel                                               │
│          ▼                                                                            │
│   ┌─────────────────────────────────────────────────────────────────────────────────┐ │
│   │                              ORDER SERVICE                                       │ │
│   │                                                                                  │ │
│   │   • Validates cancellation eligibility                                           │ │
│   │   • Handles race condition (PaymentProcessing state)                             │ │
│   │   • Updates order status                                                         │ │
│   │   • Publishes OrderCancelledEvent                                                │ │
│   └──────────────────────────────────┬───────────────────────────────────────────────┘ │
│                                      │                                                │
│                                      │ OrderCancelledEvent                            │
│                                      ▼                                                │
│                       ┌──────────────────────────────┐                                │
│                       │  SNS: gearify-order-events   │                                │
│                       └──────────────┬───────────────┘                                │
│                                      │                                                │
│                    ┌─────────────────┴─────────────────┐                              │
│                    │                                   │                              │
│                    ▼                                   ▼                              │
│     ┌──────────────────────────────┐   ┌──────────────────────────────┐               │
│     │ SQS: gearify-order-refund-   │   │ SQS: gearify-notification-   │               │
│     │ queue                        │   │ order-queue                  │               │
│     │ Filter: OrderCancelledEvent  │   │ Filter: OrderCancelledEvent  │               │
│     └──────────────┬───────────────┘   │ (only if PaymentId is null)  │               │
│                    │                   └──────────────┬───────────────┘               │
│                    │                                  │                               │
│   ┌────────────────┼────────────────┐   ┌─────────────┼─────────────────────────────┐ │
│   │ PAYMENT SERVICE│                │   │ NOTIFICATION│SERVICE                      │ │
│   │                ▼                │   │             ▼                             │ │
│   │  OrderCancelledEventHandler     │   │  OrderCancelledEventHandler               │ │
│   │    • Check if PaymentId exists  │   │    • Send OrderCancelled email            │ │
│   │    • If paid → RefundPayment    │   │      (for non-paid cancellations)         │ │
│   │                                 │   │                                           │ │
│   │  RefundPaymentCommandHandler    │   │                                           │ │
│   │    • Process refund via Stripe  │   │                                           │ │
│   │    • Publish RefundCompleted    │   │                                           │ │
│   │      or RefundFailed event      │   │                                           │ │
│   └────────────────┬────────────────┘   └───────────────────────────────────────────┘ │
│                    │                                                                  │
│                    │ RefundCompletedEvent / RefundFailedEvent                         │
│                    ▼                                                                  │
│                       ┌──────────────────────────────┐                                │
│                       │ SNS: gearify-payment-events  │                                │
│                       └──────────────┬───────────────┘                                │
│                                      │                                                │
│                    ┌─────────────────┴─────────────────┐                              │
│                    │                                   │                              │
│                    ▼                                   ▼                              │
│     ┌──────────────────────────────┐   ┌──────────────────────────────┐               │
│     │ SQS: order-payment-events-   │   │ SQS: gearify-notification-   │               │
│     │ queue (existing)             │   │ refund-queue                 │               │
│     │ Filter: + RefundCompleted    │   │ Filter: RefundCompleted,     │               │
│     └──────────────┬───────────────┘   │         RefundFailed         │               │
│                    │                   └──────────────┬───────────────┘               │
│                    │                                  │                               │
│   ┌────────────────┼────────────────┐   ┌─────────────┼─────────────────────────────┐ │
│   │ ORDER SERVICE  │                │   │ NOTIFICATION│SERVICE                      │ │
│   │                ▼                │   │             ▼                             │ │
│   │  PaymentEventHandler            │   │  RefundEventHandler                       │ │
│   │    • Handle RefundCompleted     │   │    • RefundCompleted:                     │ │
│   │    • Update order → Refunded    │   │      Send OrderCancelledRefunded email    │ │
│   │                                 │   │    • RefundFailed:                        │ │
│   │                                 │   │      Send RefundFailed email              │ │
│   │                                 │   │      + Alert admin                        │ │
│   └─────────────────────────────────┘   └───────────────────────────────────────────┘ │
│                                                                                       │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

### SNS Topics & SQS Queues

#### New Queues

| Queue Name | Subscribes To | Consumer | Event Filter |
|------------|---------------|----------|--------------|
| `gearify-order-refund-queue` | `gearify-order-events` | Payment Service | `OrderCancelledEvent` |
| `gearify-notification-refund-queue` | `gearify-payment-events` | Notification Service | `RefundCompletedEvent`, `RefundFailedEvent` |

#### Updated Existing Queues

| Queue Name | Additional Filter |
|------------|-------------------|
| `order-payment-events-queue` | Add `RefundCompletedEvent` to existing filter |
| `notification-payment-events-queue` | No change (or add for non-paid cancellation emails) |

---

## Race Condition Handling

### The Problem

When a user requests cancellation while payment is actively being processed, we face a race condition:

```
Timeline A: Cancel Request Processed First (PROBLEM)
───────────────────────────────────────────────────────────────────────────────
  User clicks       Order Service         Payment succeeds       PaymentCompleted
  "Cancel"          sets status=          at Stripe              Event arrives
                    Cancelled
     │                    │                      │                      │
     ▼                    ▼                      ▼                      ▼
   t=0                  t=50ms                t=200ms                t=300ms
                          │                                             │
                          └─── Order is Cancelled ───────────────────────┘
                                                                        │
                                                   ❌ Payment succeeded but order
                                                      is already cancelled!
                                                      Money charged, no refund triggered.
```

```
Timeline B: Payment Completes First (OK)
───────────────────────────────────────────────────────────────────────────────
  User clicks       Payment succeeds       PaymentCompleted       Cancel processed
  "Cancel"          at Stripe              Event arrives          Order→Cancelled
                                           Order→Paid             →Refund triggered
     │                    │                      │                      │
     ▼                    ▼                      ▼                      ▼
   t=0                  t=50ms                t=100ms                t=200ms
                                                                        │
                                                   ✓ Normal flow, refund happens
```

### The Solution: Cancellation Request Pattern

Instead of immediately cancelling orders in `PaymentProcessing` status, we use a **deferred cancellation** approach leveraging the existing `SagaState` field.

#### State Transitions

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              ORDER STATE MACHINE                                     │
│                                                                                      │
│                                                                                      │
│                                    ┌─────────────┐                                   │
│                                    │   Pending   │                                   │
│                                    └──────┬──────┘                                   │
│                                           │                                          │
│                                           │ Order Created                            │
│                                           ▼                                          │
│                              ┌────────────────────────┐                              │
│                              │   PaymentProcessing    │                              │
│                              │                        │                              │
│                              │  (SagaState=           │                              │
│                              │   PaymentPending)      │                              │
│                              └────────────┬───────────┘                              │
│                                           │                                          │
│              ┌────────────────────────────┼────────────────────────────┐             │
│              │                            │                            │             │
│              │                            │                            │             │
│      Cancel requested              Payment succeeds             Payment fails        │
│      ────────────────              ────────────────             ─────────────        │
│      Set SagaState=                       │                            │             │
│      Compensating                         │                            │             │
│      (don't change                        │                            │             │
│       OrderStatus yet)                    │                            │             │
│              │                            │                            │             │
│              ▼                            ▼                            ▼             │
│     ┌─────────────────┐          ┌───────────────┐          ┌───────────────┐        │
│     │ PaymentProcessing│          │     Paid      │          │ PaymentFailed │        │
│     │ (SagaState=     │          │               │          │               │        │
│     │  Compensating)  │          │               │          │               │        │
│     │                 │          │               │          │               │        │
│     │ Waiting for     │          │               │          │               │        │
│     │ payment result  │          │               │          │               │        │
│     └────────┬────────┘          └───────┬───────┘          └───────┬───────┘        │
│              │                           │                          │                │
│              │                           │ Cancel                   │ Cancel         │
│   Payment    │                           │ requested                │ requested      │
│   result     │                           │                          │                │
│   arrives    │                           ▼                          ▼                │
│              │                   ┌───────────────┐          ┌───────────────┐        │
│   ┌──────────┴──────────┐        │  Cancelled    │          │  Cancelled    │        │
│   │                     │        │               │          │               │        │
│   │                     │        │ (has PaymentId│          │ (no PaymentId)│        │
│   ▼                     ▼        │  → triggers   │          │               │        │
│ Payment              Payment     │    refund)    │          │               │        │
│ Succeeded            Failed      └───────┬───────┘          └───────────────┘        │
│   │                     │                │                                           │
│   │                     │                │ Refund                                     │
│   │                     │                │ completed                                  │
│   │                     │                ▼                                            │
│   │                     │        ┌───────────────┐                                    │
│   │                     │        │   Refunded    │                                    │
│   │                     │        └───────────────┘                                    │
│   │                     │                                                            │
│   ▼                     ▼                                                            │
│ ┌───────────────┐ ┌───────────────┐                                                  │
│ │  Cancelled    │ │  Cancelled    │                                                  │
│ │               │ │               │                                                  │
│ │ (payment      │ │ (payment      │                                                  │
│ │  succeeded    │ │  failed,      │                                                  │
│ │  → auto       │ │  no refund    │                                                  │
│ │  refund)      │ │  needed)      │                                                  │
│ └───────┬───────┘ └───────────────┘                                                  │
│         │                                                                            │
│         │ Refund completed                                                           │
│         ▼                                                                            │
│ ┌───────────────┐                                                                    │
│ │   Refunded    │                                                                    │
│ └───────────────┘                                                                    │
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

#### Decision Logic in CancelOrderCommandHandler

```csharp
public async Task<CancelOrderResult> Handle(CancelOrderCommand request, CancellationToken ct)
{
    var order = await _orderRepository.GetByIdAsync(request.OrderId, ct);

    // Validate order exists and is cancellable
    if (!CanBeCancelled(order.Status))
        return CancelOrderResult.Failed("Order cannot be cancelled in current status");

    if (order.Status == OrderStatus.PaymentProcessing)
    {
        // RACE CONDITION HANDLING:
        // Don't cancel immediately - mark for deferred cancellation
        order.SagaState = SagaState.Compensating;
        order.CancellationReason = request.Reason;
        order.CancellationRequestedAt = DateTime.UtcNow;
        order.CancellationRequestedBy = request.CancelledBy;

        await _orderRepository.UpdateAsync(order, ct);

        return CancelOrderResult.Pending(
            "Cancellation requested. Payment is being processed. " +
            "If payment succeeds, we will automatically refund it.");
    }

    // Immediate cancellation for other statuses
    var hadPayment = order.Status == OrderStatus.Paid ||
                     order.Status == OrderStatus.Processing;

    order.Status = OrderStatus.Cancelled;
    order.CancelledAt = DateTime.UtcNow;
    order.CancellationReason = request.Reason;
    order.SagaState = hadPayment ? SagaState.Compensating : SagaState.Failed;

    await _orderRepository.UpdateAsync(order, ct);

    // Publish event
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

    await _eventPublisher.PublishAsync(evt, ct);

    return CancelOrderResult.Success(order.Id, hadPayment);
}
```

#### Decision Logic in PaymentEventHandler (Order Service)

```csharp
public async Task<bool> HandleAsync(PaymentEventMessage message, CancellationToken ct)
{
    return message.EventType switch
    {
        "PaymentCompletedEvent" => await HandlePaymentCompletedAsync(message, ct),
        "PaymentFailedEvent" => await HandlePaymentFailedAsync(message, ct),
        "RefundCompletedEvent" => await HandleRefundCompletedAsync(message, ct),
        _ => true // Unknown event type, delete from queue
    };
}

private async Task<bool> HandlePaymentCompletedAsync(PaymentEventMessage message, CancellationToken ct)
{
    var order = await _orderRepository.GetByIdAsync(message.OrderId, ct);

    // CHECK FOR DEFERRED CANCELLATION
    if (order.SagaState == SagaState.Compensating)
    {
        // Cancellation was requested while payment was processing
        // Payment succeeded, so we need to cancel and refund

        order.Status = OrderStatus.Cancelled;
        order.PaymentId = message.TransactionId;
        order.PaymentStatus = "Completed";
        order.CancelledAt = DateTime.UtcNow;

        await _orderRepository.UpdateAsync(order, ct);

        // Publish OrderCancelledEvent with PaymentId to trigger refund
        var evt = new OrderCancelledEvent
        {
            TenantId = order.TenantId,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Reason = order.CancellationReason ?? "Cancelled by user",
            CancelledBy = order.CancellationRequestedBy,
            PaymentId = message.TransactionId,
            PaidAmount = message.Amount,
            Currency = message.Currency,
            OccurredAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(evt, ct);

        _logger.LogInformation(
            "Order {OrderId} was cancelled during payment processing. " +
            "Payment succeeded, triggering automatic refund.",
            order.Id);

        return true;
    }

    // Normal flow - update order to Paid
    order.Status = OrderStatus.Paid;
    order.PaymentId = message.TransactionId;
    order.PaymentStatus = "Completed";
    order.SagaState = SagaState.PaymentCompleted;

    await _orderRepository.UpdateAsync(order, ct);

    // Publish OrderConfirmedEvent for shipping...

    return true;
}

private async Task<bool> HandlePaymentFailedAsync(PaymentEventMessage message, CancellationToken ct)
{
    var order = await _orderRepository.GetByIdAsync(message.OrderId, ct);

    // CHECK FOR DEFERRED CANCELLATION
    if (order.SagaState == SagaState.Compensating)
    {
        // Cancellation was requested and payment failed
        // Good news: no refund needed!

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.SagaState = SagaState.Failed;

        await _orderRepository.UpdateAsync(order, ct);

        // Publish OrderCancelledEvent WITHOUT PaymentId (no refund needed)
        var evt = new OrderCancelledEvent
        {
            TenantId = order.TenantId,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Reason = order.CancellationReason ?? "Cancelled by user",
            CancelledBy = order.CancellationRequestedBy,
            PaymentId = null,  // No payment to refund
            PaidAmount = null,
            Currency = order.Currency,
            OccurredAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(evt, ct);

        _logger.LogInformation(
            "Order {OrderId} was cancelled during payment processing. " +
            "Payment failed, no refund needed.",
            order.Id);

        return true;
    }

    // Normal flow - update order to PaymentFailed
    order.Status = OrderStatus.PaymentFailed;
    order.PaymentStatus = "Failed";
    order.SagaState = SagaState.Failed;

    await _orderRepository.UpdateAsync(order, ct);

    return true;
}
```

---

## Event Flow Diagrams

### Flow 1: Cancel Order (Not Paid)

Applies to orders in `Pending` or `PaymentFailed` status.

```
┌─────────────┐     POST /api/orders/{id}/cancel      ┌─────────────────┐
│   CLIENT    │ ─────────────────────────────────────>│  ORDER SERVICE  │
└─────────────┘                                       └────────┬────────┘
                                                               │
                                                               │ 1. Validate status (Pending/PaymentFailed)
                                                               │ 2. Update status → Cancelled
                                                               │ 3. Publish OrderCancelledEvent
                                                               │    (PaymentId = null)
                                                               ▼
                                                  ┌──────────────────────────────┐
                                                  │  SNS: gearify-order-events   │
                                                  └──────────────┬───────────────┘
                                                                 │
                                          ┌──────────────────────┴──────────────────────┐
                                          │                                             │
                                          ▼                                             ▼
                           ┌──────────────────────────────┐          ┌──────────────────────────────┐
                           │ SQS: gearify-order-refund-   │          │ SQS: gearify-notification-   │
                           │ queue                        │          │ order-queue                  │
                           └──────────────┬───────────────┘          └──────────────┬───────────────┘
                                          │                                         │
                                          ▼                                         ▼
                           ┌──────────────────────────────┐          ┌──────────────────────────────┐
                           │ PAYMENT SERVICE              │          │ NOTIFICATION SERVICE         │
                           │                              │          │                              │
                           │ OrderCancelledEventHandler:  │          │ OrderCancelledEventHandler:  │
                           │   PaymentId is null          │          │   PaymentId is null          │
                           │   → No refund needed         │          │   → Send OrderCancelled.html │
                           │   → Delete message           │          │                              │
                           └──────────────────────────────┘          └──────────────────────────────┘
```

### Flow 2: Cancel Order (Already Paid)

Applies to orders in `Paid` or `Processing` status.

```
┌─────────────┐     POST /api/orders/{id}/cancel      ┌─────────────────┐
│   CLIENT    │ ─────────────────────────────────────>│  ORDER SERVICE  │
└─────────────┘                                       └────────┬────────┘
                                                               │
                                                               │ 1. Validate status (Paid/Processing)
                                                               │ 2. Update status → Cancelled
                                                               │ 3. Publish OrderCancelledEvent
                                                               │    (PaymentId = guid, Amount = $X)
                                                               ▼
                                                  ┌──────────────────────────────┐
                                                  │  SNS: gearify-order-events   │
                                                  └──────────────┬───────────────┘
                                                                 │
                                                                 ▼
                                          ┌──────────────────────────────┐
                                          │ SQS: gearify-order-refund-   │
                                          │ queue                        │
                                          └──────────────┬───────────────┘
                                                         │
                                                         ▼
                                          ┌──────────────────────────────┐
                                          │ PAYMENT SERVICE              │
                                          │                              │
                                          │ OrderCancelledEventHandler:  │
                                          │   1. PaymentId exists        │
                                          │   2. Send RefundPayment      │
                                          │      Command                 │
                                          │                              │
                                          │ RefundPaymentCommandHandler: │
                                          │   1. Call Stripe Refund API  │
                                          │   2. Update transaction      │
                                          │   3. Publish event           │
                                          └──────────────┬───────────────┘
                                                         │
                                     ┌───────────────────┴───────────────────┐
                                     │                                       │
                              SUCCESS│                                       │FAILURE
                                     ▼                                       ▼
                      ┌──────────────────────────┐            ┌──────────────────────────┐
                      │ RefundCompletedEvent     │            │ RefundFailedEvent        │
                      │                          │            │                          │
                      │ - RefundId               │            │ - TransactionId          │
                      │ - TransactionId          │            │ - OrderId                │
                      │ - OrderId                │            │ - ErrorCode              │
                      │ - RefundAmount           │            │ - ErrorMessage           │
                      │ - ProviderRefundId       │            │ - RetryCount             │
                      └────────────┬─────────────┘            └────────────┬─────────────┘
                                   │                                       │
                                   ▼                                       ▼
                      ┌──────────────────────────────┐      ┌──────────────────────────────┐
                      │ SNS: gearify-payment-events  │      │ SNS: gearify-payment-events  │
                      └──────────────┬───────────────┘      └──────────────┬───────────────┘
                                     │                                     │
                    ┌────────────────┴────────────────┐    ┌───────────────┴───────────────┐
                    │                                 │    │                               │
                    ▼                                 ▼    ▼                               ▼
     ┌──────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
     │ SQS: order-payment-      │   │ SQS: gearify-notification │   │ SQS: gearify-notification │
     │ events-queue             │   │ -refund-queue             │   │ -refund-queue             │
     └───────────┬──────────────┘   └───────────┬──────────────┘   └───────────┬──────────────┘
                 │                              │                              │
                 ▼                              ▼                              ▼
     ┌──────────────────────────┐   ┌──────────────────────────┐   ┌──────────────────────────┐
     │ ORDER SERVICE            │   │ NOTIFICATION SERVICE     │   │ NOTIFICATION SERVICE     │
     │                          │   │                          │   │                          │
     │ PaymentEventHandler:     │   │ RefundEventHandler:      │   │ RefundEventHandler:      │
     │   Update order status    │   │   Send                   │   │   Send RefundFailed.html │
     │   → Refunded             │   │   OrderCancelledRefunded │   │   + Alert admin          │
     │                          │   │   .html                  │   │                          │
     └──────────────────────────┘   └──────────────────────────┘   └──────────────────────────┘
```

### Flow 3: Cancel During Payment Processing (Race Condition)

```
┌─────────────┐     POST /api/orders/{id}/cancel      ┌─────────────────┐
│   CLIENT    │ ─────────────────────────────────────>│  ORDER SERVICE  │
└─────────────┘                                       └────────┬────────┘
                                                               │
                                                               │ 1. Status is PaymentProcessing
                                                               │ 2. Set SagaState = Compensating
                                                               │ 3. Store CancellationReason
                                                               │ 4. DO NOT change OrderStatus
                                                               │ 5. Return "Cancellation pending"
                                                               │
                                                               │ (No event published yet - waiting
                                                               │  for payment result)
                                                               ▼
                                          ┌────────────────────────────────────────┐
                                          │        WAITING FOR PAYMENT RESULT       │
                                          └────────────────────┬───────────────────┘
                                                               │
                                     ┌─────────────────────────┴─────────────────────────┐
                                     │                                                   │
                              PaymentCompletedEvent                              PaymentFailedEvent
                              arrives at Order Service                          arrives at Order Service
                                     │                                                   │
                                     ▼                                                   ▼
                      ┌──────────────────────────────┐              ┌──────────────────────────────┐
                      │ ORDER SERVICE                │              │ ORDER SERVICE                │
                      │                              │              │                              │
                      │ Check: SagaState ==          │              │ Check: SagaState ==          │
                      │        Compensating?         │              │        Compensating?         │
                      │                              │              │                              │
                      │ YES:                         │              │ YES:                         │
                      │   1. Status → Cancelled      │              │   1. Status → Cancelled      │
                      │   2. Store PaymentId         │              │   2. PaymentId = null        │
                      │   3. Publish                 │              │   3. Publish                 │
                      │      OrderCancelledEvent     │              │      OrderCancelledEvent     │
                      │      WITH PaymentId          │              │      WITHOUT PaymentId       │
                      │      (triggers refund)       │              │      (no refund needed)      │
                      └──────────────┬───────────────┘              └──────────────┬───────────────┘
                                     │                                             │
                                     ▼                                             ▼
                              (Continue to Flow 2                          (Continue to Flow 1
                               for refund processing)                       for notification only)
```

---

## Event Definitions

### OrderCancelledEvent

Published by Order Service when an order is cancelled.

```csharp
public record OrderCancelledEvent : IDomainEvent
{
    /// <summary>Tenant identifier for multi-tenancy</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Unique order identifier</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number (e.g., "ORD-2024-001234")</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who placed the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Reason for cancellation</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Who cancelled the order (user ID, "system", or "admin")</summary>
    public string? CancelledBy { get; init; }

    /// <summary>
    /// Payment transaction ID if order was paid.
    /// NULL if order was cancelled before payment or payment failed.
    /// When present, triggers automatic refund.
    /// </summary>
    public Guid? PaymentId { get; init; }

    /// <summary>Amount paid (for refund). NULL if no payment.</summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>Currency code (e.g., "USD")</summary>
    public string? Currency { get; init; }

    /// <summary>When the cancellation occurred</summary>
    public DateTime OccurredAt { get; init; }
}
```

**JSON Example (with payment):**
```json
{
  "eventId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "eventType": "OrderCancelledEvent",
  "tenantId": "default",
  "timestamp": "2024-01-15T10:30:00Z",
  "payload": {
    "tenantId": "default",
    "orderId": "550e8400-e29b-41d4-a716-446655440000",
    "orderNumber": "ORD-2024-001234",
    "userId": "user-123",
    "reason": "Changed my mind",
    "cancelledBy": "user-123",
    "paymentId": "660e8400-e29b-41d4-a716-446655440001",
    "paidAmount": 129.99,
    "currency": "USD",
    "occurredAt": "2024-01-15T10:30:00Z"
  }
}
```

**JSON Example (without payment):**
```json
{
  "eventId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "eventType": "OrderCancelledEvent",
  "tenantId": "default",
  "timestamp": "2024-01-15T10:30:00Z",
  "payload": {
    "tenantId": "default",
    "orderId": "550e8400-e29b-41d4-a716-446655440000",
    "orderNumber": "ORD-2024-001234",
    "userId": "user-123",
    "reason": "Changed my mind",
    "cancelledBy": "user-123",
    "paymentId": null,
    "paidAmount": null,
    "currency": "USD",
    "occurredAt": "2024-01-15T10:30:00Z"
  }
}
```

### RefundCompletedEvent

Published by Payment Service when a refund is successfully processed.

```csharp
public record RefundCompletedEvent : IDomainEvent
{
    /// <summary>Tenant identifier</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Unique refund identifier</summary>
    public Guid RefundId { get; init; }

    /// <summary>Original payment transaction ID</summary>
    public Guid TransactionId { get; init; }

    /// <summary>Order ID associated with this refund</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who owns the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Amount refunded</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>Original payment amount</summary>
    public decimal OriginalAmount { get; init; }

    /// <summary>Currency code</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Reason for refund</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Payment provider's refund ID (e.g., Stripe refund ID)</summary>
    public string? ProviderRefundId { get; init; }

    /// <summary>When the refund was completed</summary>
    public DateTime OccurredAt { get; init; }
}
```

### RefundFailedEvent

Published by Payment Service when a refund fails after retries.

```csharp
public record RefundFailedEvent : IDomainEvent
{
    /// <summary>Tenant identifier</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Original payment transaction ID</summary>
    public Guid TransactionId { get; init; }

    /// <summary>Order ID associated with this refund attempt</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who owns the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Amount that failed to refund</summary>
    public decimal Amount { get; init; }

    /// <summary>Currency code</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>Error code from payment provider</summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>Human-readable error message</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>Number of retry attempts made</summary>
    public int RetryCount { get; init; }

    /// <summary>When the final failure occurred</summary>
    public DateTime OccurredAt { get; init; }
}
```

---

## Service Responsibilities

### Order Service

| Component | Responsibility |
|-----------|----------------|
| `CancelOrderCommand` | Handle cancellation request, manage race condition |
| `CancelOrderCommandHandler` | Validate status, set deferred cancellation if needed, publish event |
| `PaymentEventHandler` | Handle `PaymentCompletedEvent`, `PaymentFailedEvent`, `RefundCompletedEvent` |
| `SnsEventPublisher` | Publish `OrderCancelledEvent` to `gearify-order-events` topic |

**Files to modify:**
- `Application/Commands/CancelOrderCommandHandler.cs` - Add race condition handling
- `Infrastructure/Messaging/PaymentEventHandler.cs` - Handle deferred cancellation, add RefundCompleted
- `Domain/Events/OrderCancelledEvent.cs` - Create new event

### Payment Service

| Component | Responsibility |
|-----------|----------------|
| `OrderCancelledEventHandler` | Listen for cancellation events, trigger refund if needed |
| `RefundPaymentCommand` | Process refund request |
| `RefundPaymentCommandHandler` | Call Stripe/PayPal refund API, publish result event |
| `SnsEventPublisher` | Publish `RefundCompletedEvent` / `RefundFailedEvent` |

**Files to create/modify:**
- `Infrastructure/Messaging/Events/Inbound/OrderCancelledEventMessage.cs` - Create message model
- `Infrastructure/Messaging/OrderCancelledEventHandler.cs` - Create handler
- `Application/Commands/RefundPaymentCommandHandler.cs` - Add event publishing
- `Startup.cs` - Register new queue consumer

### Notification Service

| Component | Responsibility |
|-----------|----------------|
| `OrderEventHandler` | Send cancellation email for non-paid orders |
| `RefundEventHandler` | Send refund confirmation/failure emails |
| `EmailTemplateService` | Render email templates |

**Files to create/modify:**
- `Infrastructure/Messaging/OrderEventHandler.cs` - Create handler for OrderCancelled
- `Infrastructure/Messaging/RefundEventHandler.cs` - Create handler for refund events
- `Infrastructure/EmailTemplates/OrderCancelled.html` - Create template
- `Infrastructure/EmailTemplates/OrderCancelledRefunded.html` - Create template
- `Infrastructure/EmailTemplates/RefundFailed.html` - Create template
- `Startup.cs` - Register new queue consumers

---

## Infrastructure Setup

### LocalStack Init Script Additions

Add to `gearify-umbrella/localstack/init-aws.sh`:

```bash
# ==========================================
# Cancel Order & Refund Flow - SQS Queues
# ==========================================
echo "  - Creating cancel/refund flow queues..."

# Payment Service queue for order cancellation events (to trigger refund)
awslocal sqs create-queue \
  --queue-name gearify-order-refund-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$ORDER_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-refund-queue already exists"

# Notification Service queue for refund events
awslocal sqs create-queue \
  --queue-name gearify-notification-refund-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-notification-refund-queue already exists"

# ==========================================
# Cancel Order & Refund Flow - SNS Subscriptions
# ==========================================
echo "  - Creating cancel/refund flow subscriptions..."

# Subscribe gearify-order-refund-queue to gearify-order-events topic
# (Payment Service listens for OrderCancelledEvent to process refunds)
if [ ! -z "$ORDER_TOPIC_ARN" ]; then
  ORDER_REFUND_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url http://localhost:4566/000000000000/gearify-order-refund-queue \
    --attribute-names QueueArn \
    --region us-east-1 \
    --output text \
    --query 'Attributes.QueueArn' 2>/dev/null || echo "")

  if [ ! -z "$ORDER_REFUND_QUEUE_ARN" ]; then
    awslocal sns subscribe \
      --topic-arn $ORDER_TOPIC_ARN \
      --protocol sqs \
      --notification-endpoint $ORDER_REFUND_QUEUE_ARN \
      --attributes '{"FilterPolicy":"{\"EventType\":[\"OrderCancelledEvent\"]}"}' \
      --region us-east-1 \
      2>/dev/null || echo "  - Failed to subscribe gearify-order-refund-queue"
    echo "  - Subscribed gearify-order-refund-queue to gearify-order-events (OrderCancelledEvent filter)"
  fi
fi

# Subscribe gearify-notification-refund-queue to gearify-payment-events topic
# (Notification Service listens for refund events to send emails)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  NOTIFICATION_REFUND_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
    --queue-url http://localhost:4566/000000000000/gearify-notification-refund-queue \
    --attribute-names QueueArn \
    --region us-east-1 \
    --output text \
    --query 'Attributes.QueueArn' 2>/dev/null || echo "")

  if [ ! -z "$NOTIFICATION_REFUND_QUEUE_ARN" ]; then
    awslocal sns subscribe \
      --topic-arn $PAYMENT_TOPIC_ARN \
      --protocol sqs \
      --notification-endpoint $NOTIFICATION_REFUND_QUEUE_ARN \
      --attributes '{"FilterPolicy":"{\"EventType\":[\"RefundCompletedEvent\",\"RefundFailedEvent\"]}"}' \
      --region us-east-1 \
      2>/dev/null || echo "  - Failed to subscribe gearify-notification-refund-queue"
    echo "  - Subscribed gearify-notification-refund-queue to gearify-payment-events (Refund events filter)"
  fi
fi

# Update order-payment-events-queue filter to include RefundCompletedEvent
# (Order Service needs to update order status to Refunded)
echo "  - Note: Update order-payment-events-queue filter to include RefundCompletedEvent"
```

### Configuration Updates

#### Payment Service - appsettings.json

```json
{
  "MessagingConfiguration": {
    "SQS": {
      "OrderRefundQueueUrl": "http://localhost:4566/000000000000/gearify-order-refund-queue"
    }
  }
}
```

#### Notification Service - appsettings.json

```json
{
  "MessagingConfiguration": {
    "SQS": {
      "RefundEventsQueueUrl": "http://localhost:4566/000000000000/gearify-notification-refund-queue"
    }
  }
}
```

---

## Email Notifications

### Template: OrderCancelled.html

**Used when:** Order cancelled before payment or payment failed.

**Placeholders:**
- `{{FirstName}}` - Customer's first name
- `{{OrderNumber}}` - Order number
- `{{CancellationReason}}` - Reason for cancellation
- `{{OrderLink}}` - Link to view order details

**Subject:** "Your Gearify Order Has Been Cancelled"

### Template: OrderCancelledRefunded.html

**Used when:** Paid order cancelled and refund completed.

**Placeholders:**
- `{{FirstName}}` - Customer's first name
- `{{OrderNumber}}` - Order number
- `{{RefundAmount}}` - Amount refunded
- `{{Currency}}` - Currency code
- `{{CancellationReason}}` - Reason for cancellation
- `{{RefundId}}` - Refund reference number
- `{{OrderLink}}` - Link to view order details

**Subject:** "Your Gearify Order Has Been Cancelled and Refunded"

### Template: RefundFailed.html

**Used when:** Refund failed after all retry attempts.

**Placeholders:**
- `{{FirstName}}` - Customer's first name
- `{{OrderNumber}}` - Order number
- `{{Amount}}` - Amount that failed to refund
- `{{Currency}}` - Currency code
- `{{ErrorMessage}}` - Brief explanation
- `{{SupportLink}}` - Link to contact support

**Subject:** "Issue Processing Your Refund - Action Required"

---

## Error Handling & Retry Strategy

### Refund Retry Flow

```
┌──────────────────────────────────────────────────────────────────────────────────────┐
│                              REFUND RETRY STRATEGY                                    │
│                                                                                       │
│  RefundPaymentCommand                                                                 │
│         │                                                                             │
│         ▼                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────────────────┐  │
│  │ RefundPaymentCommandHandler                                                      │  │
│  │                                                                                  │  │
│  │  1. Call Stripe/PayPal refund API                                                │  │
│  │                                                                                  │  │
│  │  ┌─────────────────────┐         ┌─────────────────────┐                         │  │
│  │  │      SUCCESS        │         │      FAILURE        │                         │  │
│  │  │                     │         │                     │                         │  │
│  │  │ • Update refund     │         │ • Log error         │                         │  │
│  │  │   status=Succeeded  │         │ • Return false      │                         │  │
│  │  │ • Publish           │         │   (message stays    │                         │  │
│  │  │   RefundCompleted   │         │    in queue)        │                         │  │
│  │  │   Event             │         │                     │                         │  │
│  │  │ • Delete message    │         │                     │                         │  │
│  │  └─────────────────────┘         └──────────┬──────────┘                         │  │
│  │                                             │                                    │  │
│  └─────────────────────────────────────────────┼────────────────────────────────────┘  │
│                                                │                                       │
│                                                ▼                                       │
│                               ┌─────────────────────────────────┐                      │
│                               │  SQS Visibility Timeout (5 min) │                      │
│                               │                                 │                      │
│                               │  Message becomes visible again  │                      │
│                               │  for retry                      │                      │
│                               └─────────────────┬───────────────┘                      │
│                                                 │                                      │
│                                                 │ Retry                                │
│                                                 │ (up to maxReceiveCount=3)            │
│                                                 ▼                                      │
│                               ┌─────────────────────────────────┐                      │
│                               │  After 3 failures:              │                      │
│                               │                                 │                      │
│                               │  1. Message moves to DLQ        │                      │
│                               │  2. DLQ processor detects it    │                      │
│                               │  3. Publish RefundFailedEvent   │                      │
│                               │  4. Alert admin                 │                      │
│                               └─────────────────────────────────┘                      │
│                                                                                        │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### Admin Alerting

When a refund fails after all retries:

1. **RefundFailedEvent** is published to SNS
2. **Notification Service** receives the event and:
   - Sends `RefundFailed.html` email to customer
   - Sends alert to configured admin email
   - Optionally logs to monitoring system

**Admin Alert Configuration:**

```json
{
  "AdminAlerts": {
    "RefundFailure": {
      "Enabled": true,
      "Recipients": ["admin@gearify.com", "finance@gearify.com"],
      "SlackWebhook": "https://hooks.slack.com/services/xxx/yyy/zzz"
    }
  }
}
```

---

## API Reference

### Cancel Order Endpoint

**Endpoint:** `POST /api/orders/{id}/cancel`

**Request:**
```json
{
  "reason": "Changed my mind",
  "cancelledBy": "user-123"  // Optional, defaults to authenticated user
}
```

**Response (Immediate Cancellation):**
```json
{
  "success": true,
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Cancelled",
  "refundInitiated": true,
  "message": "Order has been cancelled. A refund of $129.99 has been initiated."
}
```

**Response (Deferred Cancellation - Payment Processing):**
```json
{
  "success": true,
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "CancellationPending",
  "refundInitiated": false,
  "message": "Cancellation requested. Payment is currently being processed. If payment succeeds, we will automatically process a refund."
}
```

**Response (Cannot Cancel):**
```json
{
  "success": false,
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Shipped",
  "message": "Order cannot be cancelled. Current status: Shipped. Please contact support for assistance."
}
```

---

## Implementation Checklist

### Phase 1: Infrastructure

- [ ] Update `init-aws.sh` with new SQS queues
- [ ] Update `init-aws.sh` with SNS subscriptions
- [ ] Update Payment Service `appsettings.json`
- [ ] Update Notification Service `appsettings.json`
- [ ] Update Order Service `appsettings.json` (add RefundCompleted to filter)

### Phase 2: Order Service

- [ ] Create `OrderCancelledEvent.cs`
- [ ] Update `CancelOrderCommandHandler.cs` for race condition handling
- [ ] Update `PaymentEventHandler.cs` to check for deferred cancellation
- [ ] Update `PaymentEventHandler.cs` to handle `RefundCompletedEvent`
- [ ] Update `SnsEventPublisher.cs` to route `OrderCancelledEvent`
- [ ] Add `CancellationRequestedAt` and `CancellationRequestedBy` to Order entity (if needed)

### Phase 3: Payment Service

- [ ] Create `OrderCancelledEventMessage.cs`
- [ ] Create `OrderCancelledEventHandler.cs`
- [ ] Update `RefundPaymentCommandHandler.cs` to publish events
- [ ] Create `RefundCompletedEvent.cs` (if not exists)
- [ ] Create `RefundFailedEvent.cs` (if not exists)
- [ ] Update `SnsEventPublisher.cs` to route refund events
- [ ] Register new queue consumer in `Startup.cs`

### Phase 4: Notification Service

- [ ] Create `OrderCancelledEventMessage.cs`
- [ ] Create `OrderEventHandler.cs`
- [ ] Create `RefundEventMessage.cs`
- [ ] Create `RefundEventHandler.cs`
- [ ] Create `OrderCancelled.html` template
- [ ] Create `OrderCancelledRefunded.html` template
- [ ] Create `RefundFailed.html` template
- [ ] Update `EmailTemplateService.cs` with new subject mappings
- [ ] Register new queue consumers in `Startup.cs`
- [ ] Add admin alerting for refund failures

### Phase 5: Testing

- [ ] Unit tests for `CancelOrderCommandHandler` (all status scenarios)
- [ ] Unit tests for `OrderCancelledEventHandler` (with/without payment)
- [ ] Unit tests for `RefundPaymentCommandHandler` (success/failure)
- [ ] Integration test: Cancel unpaid order flow
- [ ] Integration test: Cancel paid order flow (with refund)
- [ ] Integration test: Race condition scenario
- [ ] Integration test: Refund failure and DLQ processing

### Phase 6: Documentation

- [x] Create `CANCEL_ORDER_REFUND.md` documentation
- [ ] Update `SNS_SQS_MESSAGING_PATTERN.md` with new events
- [ ] Update `SNS_SQS_FANOUT.md` with new flow diagrams
- [ ] Update API documentation

---

## Appendix: Order Entity Fields

Fields used for cancellation tracking:

```csharp
public class Order
{
    // ... existing fields ...

    // Cancellation tracking
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationRequestedBy { get; set; }
    public DateTime? CancellationRequestedAt { get; set; }

    // Saga state for race condition handling
    public SagaState SagaState { get; set; }
}

public enum SagaState
{
    Created,
    PaymentPending,
    PaymentCompleted,
    ShippingPending,
    ShippingCreated,
    Completed,
    Compensating,  // Used for deferred cancellation
    Failed
}
```
