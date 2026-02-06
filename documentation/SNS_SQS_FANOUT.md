# SNS/SQS Event Architecture

## Design Principle: One Queue Per Event Type

Each event type gets its own dedicated queue. This provides:
- **Scalability**: Scale each event processor independently
- **Debuggability**: Easy to trace issues (problem → queue → handler)
- **Clarity**: Handler name = what it does

```
Pattern: 1 Event Type → 1 Queue → 1 Handler → 1 Command
```

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SNS TOPICS & SQS QUEUES                            │
│                                                                              │
│  gearify-order-events (SNS)                                                  │
│  ├── gearify-order-created-queue ──────────> Payment Service                │
│  │   Filter: [OrderCreatedEvent]              OrderCreatedEventHandler      │
│  │                                            → ProcessPaymentCommand       │
│  │                                                                          │
│  └── gearify-order-cancelled-queue ────────> Payment Service                │
│      Filter: [OrderCancelledEvent]            OrderCancelledEventHandler    │
│                                               → ProcessRefundCommand        │
│                                                                              │
│  gearify-payment-events (SNS)                                                │
│  ├── gearify-payment-completed-queue ──────> Order Service                  │
│  │   Filter: [PaymentCompletedEvent]          PaymentCompletedEventHandler  │
│  │                                            → ConfirmOrderCommand         │
│  │                                                                          │
│  ├── gearify-payment-failed-queue ─────────> Order Service                  │
│  │   Filter: [PaymentFailedEvent]             PaymentFailedEventHandler     │
│  │                                            → UpdateOrderStatusCommand    │
│  │                                                                          │
│  ├── gearify-refund-completed-queue ───────> Order Service                  │
│  │   Filter: [RefundCompletedEvent]           RefundCompletedEventHandler   │
│  │                                            → Mark Order as Refunded      │
│  │                                                                          │
│  ├── gearify-notification-payment-queue ───> Notification Service           │
│  │   Filter: [PaymentCompleted, Failed]       → Send payment emails         │
│  │                                                                          │
│  └── gearify-notification-refund-queue ────> Notification Service           │
│      Filter: [RefundCompleted, Failed]        → Send refund emails          │
│                                                                              │
│  gearify-shipping-events (SNS)                                               │
│  ├── gearify-shipping-created-queue ───────> Order Service                  │
│  │   Filter: [ShipmentCreated]                → Attach shipment to order    │
│  │                                                                          │
│  └── gearify-shipping-status-queue ────────> Order Service                  │
│      Filter: [StatusUpdated, Delivered]       → Update order status         │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Flow 1: Place Order & Payment

```
Customer Places Order
        │
        ▼
┌───────────────────┐
│   ORDER SERVICE   │
│                   │
│ CreateOrderCommand│
│ → Order: Pending  │
│ → Publish Event   │
└─────────┬─────────┘
          │
          ▼
    SNS: gearify-order-events
          │
          │ Filter: OrderCreatedEvent
          ▼
    SQS: gearify-order-created-queue
          │
          ▼
┌───────────────────┐
│  PAYMENT SERVICE  │
│                   │
│ OrderCreatedEvent │
│ Handler           │
│ → Process Payment │
│ → Publish Result  │
└─────────┬─────────┘
          │
          ▼
    SNS: gearify-payment-events
          │
    ┌─────┴─────┐
    │           │
    ▼           ▼
SQS: payment-  SQS: notification-
completed-     payment-queue
queue          │
    │          ▼
    ▼     ┌──────────────────┐
┌──────────────────┐         │ NOTIFICATION SVC │
│  ORDER SERVICE   │         │ → Send email     │
│                  │         └──────────────────┘
│ PaymentCompleted │
│ EventHandler     │
│ → Confirm Order  │
└──────────────────┘
```

---

## Flow 2: Cancel Order & Refund

```
Customer Cancels Order
        │
        ▼
┌───────────────────┐
│   ORDER SERVICE   │
│                   │
│ CancelOrderCommand│
│ → Order: Cancelled│
│ → Publish Event   │
│   (with PaymentId)│
└─────────┬─────────┘
          │
          ▼
    SNS: gearify-order-events
          │
          │ Filter: OrderCancelledEvent
          ▼
    SQS: gearify-order-cancelled-queue
          │
          ▼
┌───────────────────┐
│  PAYMENT SERVICE  │
│                   │
│ OrderCancelled    │
│ EventHandler      │
│ → Check PaymentId │
│ → If paid: Refund │
│ → Publish Result  │
└─────────┬─────────┘
          │
          ▼
    SNS: gearify-payment-events
          │
    ┌─────┴─────┐
    │           │
    ▼           ▼
SQS: refund-   SQS: notification-
completed-     refund-queue
queue          │
    │          ▼
    ▼     ┌──────────────────┐
┌──────────────────┐         │ NOTIFICATION SVC │
│  ORDER SERVICE   │         │ → Send email     │
│                  │         └──────────────────┘
│ RefundCompleted  │
│ EventHandler     │
│ → Mark Refunded  │
└──────────────────┘
```

---

## Code Structure

```
gearify-payment-svc/
├── Startup.cs
│   services.AddEventQueueProcessor<OrderCreatedEvent, OrderCreatedEventHandler>(
│       config.SQS.OrderCreatedQueueUrl);
│   services.AddEventQueueProcessor<OrderCancelledEvent, OrderCancelledEventHandler>(
│       config.SQS.OrderCancelledQueueUrl);
│
└── Infrastructure/Messaging/
    ├── Events/Inbound/
    │   ├── OrderCreatedEvent.cs       # Simple DTO
    │   └── OrderCancelledEvent.cs     # Simple DTO
    └── Handlers/
        ├── OrderCreatedEventHandler.cs   # → ProcessPaymentCommand
        └── OrderCancelledEventHandler.cs # → ProcessRefundCommand

gearify-order-svc/
├── Startup.cs
│   services.AddEventQueueProcessor<PaymentCompletedEvent, PaymentCompletedEventHandler>(...);
│   services.AddEventQueueProcessor<PaymentFailedEvent, PaymentFailedEventHandler>(...);
│   services.AddEventQueueProcessor<RefundCompletedEvent, RefundCompletedEventHandler>(...);
│
└── Infrastructure/Messaging/
    ├── Events/Inbound/
    │   ├── PaymentCompletedEvent.cs
    │   ├── PaymentFailedEvent.cs
    │   └── RefundCompletedEvent.cs
    └── Handlers/
        ├── PaymentCompletedEventHandler.cs
        ├── PaymentFailedEventHandler.cs
        └── RefundCompletedEventHandler.cs
```

---

## Quick Reference: Queue Mapping

| SNS Topic | SQS Queue | Consumer | Event Types |
|-----------|-----------|----------|-------------|
| gearify-order-events | gearify-order-created-queue | Payment Service | OrderCreatedEvent |
| gearify-order-events | gearify-order-cancelled-queue | Payment Service | OrderCancelledEvent |
| gearify-payment-events | gearify-payment-completed-queue | Order Service | PaymentCompletedEvent |
| gearify-payment-events | gearify-payment-failed-queue | Order Service | PaymentFailedEvent |
| gearify-payment-events | gearify-refund-completed-queue | Order Service | RefundCompletedEvent |
| gearify-payment-events | gearify-notification-payment-events-queue | Notification Service | PaymentCompleted, PaymentFailed |
| gearify-payment-events | gearify-notification-refund-queue | Notification Service | RefundCompleted, RefundFailed |
| gearify-shipping-events | gearify-shipping-created-queue | Order Service | ShipmentCreated |
| gearify-shipping-events | gearify-shipping-status-queue | Order Service | ShipmentStatusUpdated, ShipmentDelivered |

---

## Dead Letter Queues

| DLQ | Failed Messages From |
|-----|---------------------|
| gearify-order-events-dlq | gearify-order-created-queue, gearify-order-cancelled-queue |
| gearify-payment-events-dlq | All payment-related queues |
| gearify-shipping-events-dlq | All shipping-related queues |

---

## Extension Method Usage

The `AddEventQueueProcessor` extension method simplifies queue registration:

```csharp
// SharedKernel provides this extension
services.AddEventQueueProcessor<TEvent, THandler>(queueUrl);

// This wires up:
// - IEventQueue<TEvent> using SqsEventQueue<TEvent>
// - IEventHandler<TEvent> using THandler
// - EventQueueProcessor<TEvent> as background service
```

No custom queue classes needed. Just:
1. DTO (event record)
2. Handler (implements IEventHandler<T>)
3. One line in Startup.cs
