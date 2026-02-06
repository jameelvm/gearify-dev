# SNS/SQS Messaging Pattern

## Overview

This document describes the standardized event-driven messaging pattern used across all Gearify microservices for asynchronous inter-service communication via AWS SNS and SQS.

All services use a consistent **EventEnvelope** pattern that wraps domain events with metadata for routing and idempotency.

### Design Principle: One Queue Per Event Type

Each event type gets its own dedicated queue with SNS filter policies handling routing:

```
Pattern: 1 Event Type → 1 Queue → 1 Handler → 1 Command
```

This provides:
- **Scalability**: Scale each event processor independently
- **Debuggability**: Easy to trace issues (problem → queue → handler)
- **Clarity**: Handler name = what it does

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                                    PRODUCER SIDE                                         │
│                                                                                          │
│  ┌──────────────────┐    ┌──────────────────────┐    ┌─────────────────────────────┐   │
│  │  Domain Event    │    │  SnsEventPublisherBase│    │      EventEnvelope          │   │
│  │                  │───>│  (SharedKernel)       │───>│  {                          │   │
│  │ PaymentCompleted │    │                       │    │    eventId: "guid",         │   │
│  │ Event            │    │  - Wraps in envelope  │    │    eventType: "Payment...", │   │
│  │                  │    │  - Adds metadata      │    │    tenantId: "tenant-1",    │   │
│  └──────────────────┘    │  - Routes to topic    │    │    timestamp: "2024-...",   │   │
│                          └──────────┬───────────┘    │    payload: { ... }         │   │
│                                     │                 │  }                          │   │
│                                     │                 └─────────────────────────────┘   │
└─────────────────────────────────────┼───────────────────────────────────────────────────┘
                                      │
                                      │ Publish (with EventType attribute)
                                      ▼
                         ┌────────────────────────┐
                         │                        │
                         │      AWS SNS Topic     │
                         │   (payment-events)     │
                         │                        │
                         └───────────┬────────────┘
                                     │
           ┌─────────────────────────┼─────────────────────────┐
           │ Filter: PaymentCompleted│ Filter: PaymentFailed   │ Filter: RefundCompleted
           ▼                         ▼                         ▼
   ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐
   │ payment-completed   │  │ payment-failed      │  │ refund-completed    │
   │ -queue              │  │ -queue              │  │ -queue              │
   └──────────┬──────────┘  └──────────┬──────────┘  └──────────┬──────────┘
              │                        │                        │
┌─────────────┼────────────────────────┼────────────────────────┼─────────────────────────┐
│             │                        │                        │       CONSUMER SIDE      │
│             ▼                        ▼                        ▼                          │
│  ┌───────────────────────────────────────────────────────────────────────────────────┐  │
│  │                        SqsEventQueue<T> (SharedKernel)                             │  │
│  │                                                                                    │  │
│  │  1. Long-poll SQS for messages                                                     │  │
│  │  2. Unwrap SNS envelope → EventEnvelope                                            │  │
│  │  3. Deserialize Payload → T (no filtering needed - SNS handles it)                 │  │
│  └────────────────────────────────────────────┬──────────────────────────────────────┘  │
│                                               │                                          │
│                                               ▼                                          │
│  ┌───────────────────────────────────────────────────────────────────────────────────┐  │
│  │                    EventQueueProcessor<T> (SharedKernel)                           │  │
│  │                                                                                    │  │
│  │  - BackgroundService that polls IEventQueue<T>                                     │  │
│  │  - Creates DI scope per polling cycle                                              │  │
│  │  - Delegates to IEventHandler<T>                                                   │  │
│  │  - Deletes messages on success                                                     │  │
│  └────────────────────────────────────────────┬──────────────────────────────────────┘  │
│                                               │                                          │
│                                               ▼                                          │
│  ┌───────────────────────────────────────────────────────────────────────────────────┐  │
│  │                      IEventHandler<T> (Service-specific)                           │  │
│  │                                                                                    │  │
│  │  - Contains business logic for ONE event type                                      │  │
│  │  - Sends MediatR commands                                                          │  │
│  │  - Returns true (delete) or false (retry)                                          │  │
│  └───────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                          │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Component Reference

### SharedKernel Components

All shared messaging infrastructure lives in `Gearify.SharedKernel`:

```
gearify-shared-kernel/
├── Events/
│   ├── IDomainEvent.cs           # Base interface for domain events
│   ├── ISnsEventPublisher.cs     # Publisher interface
│   ├── EventEnvelope.cs          # Standard event wrapper
│   └── SnsEventPublisherBase.cs  # Base publisher class
│
├── Extensions/
│   └── EventQueueExtensions.cs   # AddEventQueueProcessor<TEvent, THandler>()
│
└── Messaging/
    ├── IEventQueue.cs            # Queue consumer interface
    ├── IEventHandler.cs          # Handler interface
    ├── QueueMessage.cs           # Message wrapper DTO
    ├── SqsEventQueue.cs          # Generic SQS consumer
    └── EventQueueProcessor.cs    # Background polling service
```

---

### 1. EventEnvelope

**Location:** `gearify-shared-kernel/Events/EventEnvelope.cs`

**Purpose:** Standardized wrapper for all domain events published via SNS. Provides consistent metadata for routing, filtering, and idempotency.

```csharp
public class EventEnvelope
{
    public string EventId { get; set; }      // Unique ID (GUID) for idempotency
    public string EventType { get; set; }    // Event class name (e.g., "PaymentCompletedEvent")
    public string TenantId { get; set; }     // Multi-tenancy support
    public DateTime Timestamp { get; set; }  // When the event occurred
    public object? Payload { get; set; }     // The actual domain event data
}
```

**Example JSON:**
```json
{
  "eventId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "eventType": "PaymentCompletedEvent",
  "tenantId": "tenant-acme",
  "timestamp": "2024-01-15T10:30:00Z",
  "payload": {
    "transactionId": "txn-123",
    "orderId": "order-456",
    "amount": 99.99,
    "currency": "USD"
  }
}
```

---

### 2. SnsEventPublisherBase

**Location:** `gearify-shared-kernel/Events/SnsEventPublisherBase.cs`

**Purpose:** Abstract base class for publishing domain events to SNS. Handles envelope wrapping, JSON serialization, and message attributes.

```csharp
public abstract class SnsEventPublisherBase : ISnsEventPublisher
{
    // Subclasses must implement to provide topic routing
    protected abstract string? GetTopicArn(string eventType);

    // Optional: Override to customize tenant extraction
    protected virtual string GetTenantId<TEvent>(TEvent domainEvent);

    // Main publish method - wraps event in envelope and publishes to SNS
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct);
}
```

**Responsibilities:**
- Wraps domain event in `EventEnvelope`
- Extracts `TenantId` via reflection (looks for `TenantId` property)
- Serializes to JSON with camelCase naming
- Adds `EventType` message attribute for SNS filtering
- Routes to correct topic via `GetTopicArn()`
- Logs success/failure (never throws on publish failure)

---

### 3. SqsEventQueue\<T\>

**Location:** `gearify-shared-kernel/Messaging/SqsEventQueue.cs`

**Purpose:** Generic SQS consumer that handles SNS envelope unwrapping and EventEnvelope deserialization.

```csharp
public class SqsEventQueue<T> : IEventQueue<T> where T : class
{
    // Simplified constructor (recommended with SNS filter policies)
    public SqsEventQueue(
        IAmazonSQS sqsClient,
        string queueUrl,
        ILogger<SqsEventQueue<T>> logger);

    public Task<List<QueueMessage<T>>> ReceiveMessagesAsync(...);
    public Task DeleteMessageAsync(string receiptHandle, ...);
}
```

**Parameters:**
| Parameter | Description |
|-----------|-------------|
| `sqsClient` | AWS SQS client instance |
| `queueUrl` | SQS queue URL to consume from |
| `logger` | Logger for diagnostics |

> **Note:** With the "one queue per event type" architecture, SNS filter policies handle event routing.
> Each queue receives only one event type, so code-level filtering is not needed.

**Message Processing Flow:**
```
SQS Message
    │
    ▼
┌─────────────────────────────────┐
│ 1. Parse SNS Envelope           │
│    { "Message": "...", ... }    │
└─────────────────┬───────────────┘
                  │
                  ▼
┌─────────────────────────────────┐
│ 2. Parse EventEnvelope          │
│    { eventType, payload, ... }  │
└─────────────────┬───────────────┘
                  │
                  ▼
┌─────────────────────────────────┐
│ 3. Deserialize Payload → T      │
└─────────────────┬───────────────┘
                  │
                  ▼
         Return QueueMessage<T>
```

---

### 4. EventQueueProcessor\<T\>

**Location:** `gearify-shared-kernel/Messaging/EventQueueProcessor.cs`

**Purpose:** Generic `BackgroundService` that continuously polls `IEventQueue<T>` and delegates to `IEventHandler<T>`.

**Behavior:**
- Runs as a hosted background service
- Creates a new DI scope per polling cycle
- Long-polls with 20-second wait time, 10 messages per batch
- Processes messages sequentially
- Deletes messages when handler returns `true`
- On exception: logs error, waits 30 seconds, resumes polling
- Messages that fail stay in queue for SQS retry/DLQ

---

### 5. IEventHandler\<T\>

**Location:** `gearify-shared-kernel/Messaging/IEventHandler.cs`

**Purpose:** Interface for service-specific event handling logic.

```csharp
public interface IEventHandler<T>
{
    Task<bool> HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

**Return Value:**
- `true` → Message processed successfully; delete from queue
- `false` → Message should remain in queue for retry

---

## Message Flow Sequence Diagram

```
┌──────────────┐  ┌────────────────────┐  ┌─────────┐  ┌─────────┐  ┌─────────────────┐  ┌───────────────┐  ┌──────────────┐
│   Domain     │  │ SnsEventPublisher  │  │   SNS   │  │   SQS   │  │EventQueueProc.  │  │ IEventHandler │  │   MediatR    │
│   Logic      │  │ Base               │  │  Topic  │  │  Queue  │  │ <T>             │  │ <T>           │  │   Command    │
└──────┬───────┘  └─────────┬──────────┘  └────┬────┘  └────┬────┘  └────────┬────────┘  └───────┬───────┘  └──────┬───────┘
       │                    │                   │            │               │                   │                 │
       │  Publish Event     │                   │            │               │                   │                 │
       │───────────────────>│                   │            │               │                   │                 │
       │                    │                   │            │               │                   │                 │
       │                    │ Wrap in Envelope  │            │               │                   │                 │
       │                    │ + Publish         │            │               │                   │                 │
       │                    │──────────────────>│            │               │                   │                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │  Fan-out   │               │                   │                 │
       │                    │                   │───────────>│               │                   │                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │  Long Poll    │                   │                 │
       │                    │                   │            │<──────────────│                   │                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │  Messages     │                   │                 │
       │                    │                   │            │──────────────>│                   │                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │               │  HandleAsync(msg) │                 │
       │                    │                   │            │               │──────────────────>│                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │               │                   │  Send(Command)  │
       │                    │                   │            │               │                   │────────────────>│
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │               │                   │     Result      │
       │                    │                   │            │               │                   │<────────────────│
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │               │  return true      │                 │
       │                    │                   │            │               │<──────────────────│                 │
       │                    │                   │            │               │                   │                 │
       │                    │                   │            │ DeleteMessage │                   │                 │
       │                    │                   │            │<──────────────│                   │                 │
```

---

## Event Communication Map

### Complete Service Communication Diagram

```
┌──────────────────────────────────────────────────────────────────────────────────────────────┐
│                              GEARIFY EVENT COMMUNICATION MAP                                  │
│                                                                                               │
│  ┌─────────────┐         gearify-order-events              ┌─────────────────┐               │
│  │   ORDER     │ ─────────────────────────────────────────>│  SNS Topic      │               │
│  │   SERVICE   │  (OrderCreatedEvent,                      │                 │               │
│  │             │   OrderCancelledEvent)                    └────────┬────────┘               │
│  │             │                                        ┌───────────┴───────────┐            │
│  │             │                                        ▼                       ▼            │
│  │             │                            gearify-order-         gearify-order-            │
│  │             │                            created-queue          cancelled-queue           │
│  │             │                                   │                       │                 │
│  │             │◄── payment-completed-queue ◄──┐   └───────────┬───────────┘                 │
│  │             │◄── payment-failed-queue ◄─────┤               ▼                             │
│  │             │◄── refund-completed-queue ◄───┤    ┌─────────────────┐                      │
│  └─────────────┘                               │    │   PAYMENT       │                      │
│                                                │    │   SERVICE       │                      │
│  ┌─────────────┐     gearify-payment-events    │    │   (Consumer)    │                      │
│  │  PAYMENT    │ ──────────────────────────────┼───>└─────────────────┘                      │
│  │  SERVICE    │  (PaymentCompletedEvent,      │            │                                │
│  │             │   PaymentFailedEvent,         │            │ gearify-payment-events         │
│  │             │   RefundCompletedEvent)       │            ▼                                │
│  │             │                               │    ┌─────────────────┐                      │
│  └─────────────┘                               │    │   SNS Topic     │                      │
│                                                │    └────────┬────────┘                      │
│                                                │    ┌────────┼────────┬────────────┐         │
│                                                │    ▼        ▼        ▼            ▼         │
│                                                │ payment- payment- refund-    notification-  │
│                                                │ completed failed   completed  queues        │
│                                                │ -queue   -queue   -queue                    │
│                                                │    │        │        │                      │
│                                                └────┴────────┴────────┘                      │
│                                                                                               │
│                                                  ┌─────────────────┐                         │
│                                                  │  NOTIFICATION   │                         │
│                                                  │  SERVICE        │◄── notification-        │
│                                                  │                 │    payment-events-queue │
│                                                  │                 │◄── notification-        │
│                                                  │                 │    refund-queue         │
│                                                  └─────────────────┘                         │
│                                                                                               │
│  ┌─────────────┐      gearify-media-upload-events       ┌─────────────────┐                  │
│  │   MEDIA     │ ─────────────────────────────────────> │  SNS Topic      │                  │
│  │   SERVICE   │  (MediaUploadedEvent)                  └────────┬────────┘                  │
│  │             │                                                 │                           │
│  │             │◄── gearify-image-processing-queue ◄─────────────┘                           │
│  │             │                                                                             │
│  │             │      gearify-image-processing-completed                                     │
│  │             │ ─────────────────────────────────────> ┌────────────────┐                   │
│  │             │  (ImageProcessingCompletedEvent)       │  SNS Topic     │                   │
│  └─────────────┘                                        └───────┬────────┘                   │
│                                                                 │                            │
│                                                                 ▼                            │
│                                       gearify-product-thumbnail-update-queue                 │
│                                                                 │                            │
│                                                                 ▼                            │
│  ┌─────────────┐       catalog-events-topic            ┌─────────────────┐                   │
│  │  CATALOG    │ ─────────────────────────────────────>│  SNS Topic      │                   │
│  │  SERVICE    │  (ProductCreatedEvent,                │                 │                   │
│  │             │   ProductUpdatedEvent,                └────────┬────────┘                   │
│  │             │   ProductDeletedEvent)                         │                            │
│  │             │                                                ▼                            │
│  │             │◄── gearify-product-thumbnail-    gearify-search-catalog-events-queue        │
│  │             │    update-queue                                │                            │
│  └─────────────┘                                                ▼                            │
│                                                         ┌─────────────────┐                  │
│                                                         │  SEARCH         │                  │
│                                                         │  SERVICE        │                  │
│                                                         │  (Consumer only)│                  │
│                                                         └─────────────────┘                  │
│                                                                                               │
│  ┌─────────────┐      gearify-shipping-events           ┌────────────────┐                   │
│  │  SHIPPING   │ ─────────────────────────────────────> │  SNS Topic     │                   │
│  │  SERVICE    │  (ShipmentCreated,                     └───────┬────────┘                   │
│  │             │   ShipmentStatusUpdated,                       │                            │
│  │             │   ShipmentDelivered)                  ┌────────┴────────┐                   │
│  └─────────────┘                                       ▼                ▼                    │
│                                            gearify-shipping-   gearify-shipping-             │
│                                            created-queue       status-queue                  │
│                                                   │                    │                     │
│                                                   └────────┬───────────┘                     │
│                                                            ▼                                 │
│                                                    ┌─────────────────┐                       │
│                                                    │  ORDER SERVICE  │                       │
│                                                    │  (Consumer)     │                       │
│                                                    └─────────────────┘                       │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### SNS Topics

| # | SNS Topic Name | Topic ARN | Publisher Service | Events Published |
|---|---------------|-----------|-------------------|------------------|
| 1 | `gearify-order-events` | `arn:aws:sns:us-east-1:000000000000:gearify-order-events` | **Order Service** | `OrderCreatedEvent`, `OrderCancelledEvent` |
| 2 | `gearify-payment-events` | `arn:aws:sns:us-east-1:000000000000:gearify-payment-events` | **Payment Service** | `PaymentCompletedEvent`, `PaymentFailedEvent`, `RefundCompletedEvent`, `RefundFailedEvent` |
| 3 | `gearify-shipping-events` | `arn:aws:sns:us-east-1:000000000000:gearify-shipping-events` | **Shipping Service** | `ShipmentCreated`, `ShipmentStatusUpdated`, `ShipmentDelivered` |
| 4 | `gearify-media-upload-events` | `arn:aws:sns:us-east-1:000000000000:gearify-media-upload-events` | **Media Service** | `MediaUploadedEvent` |
| 5 | `gearify-image-processing-completed` | `arn:aws:sns:us-east-1:000000000000:gearify-image-processing-completed` | **Media Service** | `ImageProcessingCompletedEvent` |
| 6 | `catalog-events-topic` | `arn:aws:sns:us-east-1:000000000000:catalog-events-topic` | **Catalog Service** | `ProductCreatedEvent`, `ProductUpdatedEvent`, `ProductDeletedEvent` |

---

### SQS Queues & Subscriptions

| # | SQS Queue Name | Subscribes To (SNS Topic) | Consumer Service | SNS Filter Policy | Handler |
|---|---------------|---------------------------|------------------|-------------------|---------|
| 1 | `gearify-order-created-queue` | `gearify-order-events` | **Payment Service** | `OrderCreatedEvent` | `OrderCreatedEventHandler` |
| 2 | `gearify-order-cancelled-queue` | `gearify-order-events` | **Payment Service** | `OrderCancelledEvent` | `OrderCancelledEventHandler` |
| 3 | `gearify-payment-completed-queue` | `gearify-payment-events` | **Order Service** | `PaymentCompletedEvent` | `PaymentCompletedEventHandler` |
| 4 | `gearify-payment-failed-queue` | `gearify-payment-events` | **Order Service** | `PaymentFailedEvent` | `PaymentFailedEventHandler` |
| 5 | `gearify-refund-completed-queue` | `gearify-payment-events` | **Order Service** | `RefundCompletedEvent` | `RefundCompletedEventHandler` |
| 6 | `gearify-notification-payment-events-queue` | `gearify-payment-events` | **Notification Service** | `PaymentCompletedEvent`, `PaymentFailedEvent` | `PaymentEventHandler` |
| 7 | `gearify-notification-refund-queue` | `gearify-payment-events` | **Notification Service** | `RefundCompletedEvent`, `RefundFailedEvent` | `RefundEventHandler` |
| 8 | `gearify-shipping-created-queue` | `gearify-shipping-events` | **Order Service** | `ShipmentCreated` | `ShipmentCreatedEventHandler` |
| 9 | `gearify-shipping-status-queue` | `gearify-shipping-events` | **Order Service** | `ShipmentStatusUpdated`, `ShipmentDelivered` | `ShippingStatusEventHandler` |
| 10 | `gearify-image-processing-queue` | `gearify-media-upload-events` | **Media Service** | *(all)* | `ImageProcessingEventHandler` |
| 11 | `gearify-product-thumbnail-update-queue` | `gearify-image-processing-completed` | **Catalog Service** | *(all)* | `ThumbnailUpdateEventHandler` |
| 12 | `gearify-search-catalog-events-queue` | `catalog-events-topic` | **Search Service** | *(all)* | `CatalogEventHandler` |

---

### Fan-Out: Topics With Multiple Subscribers

The following topics deliver to multiple queues using SNS filter policies:

**`gearify-payment-events`** (5 subscribers)

```
Payment Service
      │
      │ publishes PaymentCompletedEvent / PaymentFailedEvent / RefundCompletedEvent
      ▼
┌──────────────────────────┐
│ gearify-payment-events   │
│ (SNS Topic)              │
└─────────────┬────────────┘
              │
    ┌─────────┼─────────┬──────────┬────────────────┬────────────────┐
    │         │         │          │                │                │
    ▼         ▼         ▼          ▼                ▼                ▼
┌────────┐ ┌────────┐ ┌────────┐ ┌────────────┐ ┌────────────┐
│payment-│ │payment-│ │refund- │ │notification│ │notification│
│complet-│ │failed- │ │complet-│ │-payment-   │ │-refund-    │
│ed-queue│ │queue   │ │ed-queue│ │events-queue│ │queue       │
└───┬────┘ └───┬────┘ └───┬────┘ └─────┬──────┘ └─────┬──────┘
    │          │          │            │              │
    │          │          │            │              │
    ▼          ▼          ▼            ▼              ▼
 Order      Order      Order     Notification   Notification
 Service    Service    Service    Service        Service
 (Confirm)  (Fail)     (Refund)  (Email)        (Email)
```

**`gearify-shipping-events`** (2 subscribers)

```
Shipping Service
      │
      │ publishes ShipmentCreated / StatusUpdated / Delivered
      ▼
┌──────────────────────────┐
│ gearify-shipping-events  │
│ (SNS Topic)              │
└─────────────┬────────────┘
              │
     ┌────────┴──────────────────────────┐
     │                                   │
     ▼                                   ▼
┌──────────────────────────┐   ┌──────────────────────────────────┐
│gearify-shipping-created  │   │gearify-shipping-status-queue     │
│-queue                    │   │                                  │
│                          │   │                                  │
│ SNS Filter:              │   │ SNS Filter:                      │
│   ShipmentCreated        │   │   ShipmentStatusUpdated,         │
│                          │   │   ShipmentDelivered              │
│ → Order Service          │   │ → Order Service                  │
│   Attach shipment        │   │   Update order status            │
└──────────────────────────┘   └──────────────────────────────────┘
```

---

### Dead Letter Queues (DLQs)

| DLQ Name | Protects Queues | Max Receive Count |
|----------|-----------------|-------------------|
| `gearify-order-events-dlq` | `gearify-order-created-queue`, `gearify-order-cancelled-queue` | 3 |
| `gearify-payment-events-dlq` | `gearify-payment-completed-queue`, `gearify-payment-failed-queue`, `gearify-refund-completed-queue`, notification queues | 3 |
| `gearify-shipping-events-dlq` | `gearify-shipping-created-queue`, `gearify-shipping-status-queue` | 3 |

---

### Quick Reference: "Where does this event go?"

| Event | Published By | SNS Topic | Queue → Consumer |
|-------|-------------|-----------|------------------|
| `OrderCreatedEvent` | Order Service | `gearify-order-events` | `gearify-order-created-queue` → Payment Service |
| `OrderCancelledEvent` | Order Service | `gearify-order-events` | `gearify-order-cancelled-queue` → Payment Service |
| `PaymentCompletedEvent` | Payment Service | `gearify-payment-events` | `gearify-payment-completed-queue` → Order Service |
| `PaymentFailedEvent` | Payment Service | `gearify-payment-events` | `gearify-payment-failed-queue` → Order Service |
| `RefundCompletedEvent` | Payment Service | `gearify-payment-events` | `gearify-refund-completed-queue` → Order Service |
| `PaymentCompletedEvent` | Payment Service | `gearify-payment-events` | `gearify-notification-payment-events-queue` → Notification Service |
| `PaymentFailedEvent` | Payment Service | `gearify-payment-events` | `gearify-notification-payment-events-queue` → Notification Service |
| `RefundCompletedEvent` | Payment Service | `gearify-payment-events` | `gearify-notification-refund-queue` → Notification Service |
| `ShipmentCreated` | Shipping Service | `gearify-shipping-events` | `gearify-shipping-created-queue` → Order Service |
| `ShipmentStatusUpdated` | Shipping Service | `gearify-shipping-events` | `gearify-shipping-status-queue` → Order Service |
| `ShipmentDelivered` | Shipping Service | `gearify-shipping-events` | `gearify-shipping-status-queue` → Order Service |
| `MediaUploadedEvent` | Media Service | `gearify-media-upload-events` | `gearify-image-processing-queue` → Media Service |
| `ImageProcessingCompletedEvent` | Media Service | `gearify-image-processing-completed` | `gearify-product-thumbnail-update-queue` → Catalog Service |
| `ProductCreatedEvent` | Catalog Service | `catalog-events-topic` | `gearify-search-catalog-events-queue` → Search Service |
| `ProductUpdatedEvent` | Catalog Service | `catalog-events-topic` | `gearify-search-catalog-events-queue` → Search Service |
| `ProductDeletedEvent` | Catalog Service | `catalog-events-topic` | `gearify-search-catalog-events-queue` → Search Service |

---

## How to Add a New Event Publisher

### Step 1: Create the Domain Event

**Location:** `{service}/Domain/Events/` or `{service}/Events/`

```csharp
using Gearify.SharedKernel.Events;

namespace YourService.Domain.Events;

public record YourDomainEvent(
    string TenantId,          // Required for multi-tenancy
    string EntityId,
    string SomeData,
    DateTime OccurredAt       // Required by IDomainEvent
) : IDomainEvent;
```

### Step 2: Create the Publisher (if service doesn't have one)

**Location:** `{service}/Infrastructure/Messaging/SnsEventPublisher.cs`

```csharp
using Amazon.SimpleNotificationService;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YourService.Infrastructure.Messaging;

public class SnsEventPublisher : SnsEventPublisherBase
{
    private readonly MessagingConfiguration _settings;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingConfiguration> settings,
        ILogger<SnsEventPublisher> logger)
        : base(snsClient, logger)
    {
        _settings = settings.Value;
    }

    protected override string? GetTopicArn(string eventType)
    {
        // Route different events to different topics (if needed)
        return eventType switch
        {
            nameof(YourDomainEvent) => _settings.SNS.YourEventsTopicArn,
            nameof(AnotherEvent) => _settings.SNS.AnotherTopicArn,
            _ => _settings.SNS.DefaultTopicArn  // Or return null to skip
        };
    }
}
```

### Step 3: Register in Startup.cs

```csharp
// SNS Client
services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
{
    var config = new AmazonSimpleNotificationServiceConfig
    {
        ServiceURL = Environment.GetEnvironmentVariable("SNS_ENDPOINT") ?? "http://localhost:4566"
    };
    return new AmazonSimpleNotificationServiceClient(config);
});

// Publisher
services.AddScoped<ISnsEventPublisher, SnsEventPublisher>();
```

### Step 4: Publish Events

```csharp
public class YourCommandHandler
{
    private readonly ISnsEventPublisher _eventPublisher;

    public async Task Handle(YourCommand command, CancellationToken ct)
    {
        // ... business logic ...

        var domainEvent = new YourDomainEvent(
            TenantId: tenantId,
            EntityId: entity.Id,
            SomeData: "value",
            OccurredAt: DateTime.UtcNow);

        await _eventPublisher.PublishAsync(domainEvent, ct);
    }
}
```

---

## How to Add a New Event Consumer

### Step 1: Define the Inbound Event DTO

**Location:** `{service}/Infrastructure/Messaging/Events/Inbound/`

Each event type gets its own simple DTO matching the domain event payload:

```csharp
namespace YourService.Infrastructure.Messaging.Events.Inbound;

public record YourDomainEvent
{
    // Fields matching the domain event's payload
    public Guid EntityId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string SomeData { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}
```

### Step 2: Implement the Event Handler

**Location:** `{service}/Infrastructure/Messaging/Handlers/YourDomainEventHandler.cs`

One handler per event type - keeps it simple and debuggable:

```csharp
using Gearify.SharedKernel.Messaging;
using MediatR;

namespace YourService.Infrastructure.Messaging.Handlers;

public class YourDomainEventHandler : IEventHandler<YourDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<YourDomainEventHandler> _logger;

    public YourDomainEventHandler(IMediator mediator, ILogger<YourDomainEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(YourDomainEvent message, CancellationToken ct)
    {
        _logger.LogInformation("Processing YourDomainEvent for entity {EntityId}", message.EntityId);

        try
        {
            var command = new ProcessYourEventCommand(
                message.EntityId,
                message.SomeData,
                message.TenantId);

            await _mediator.Send(command, ct);
            return true;  // Success - delete from queue
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process YourDomainEvent for {EntityId}", message.EntityId);
            return false;  // Failure - keep in queue for retry
        }
    }
}
```

### Step 3: Register in Startup.cs (One Line!)

Using the `AddEventQueueProcessor` extension method:

```csharp
using Gearify.SharedKernel.Extensions;

// In ConfigureServices():

var messagingConfig = Configuration.GetSection("MessagingConfiguration").Get<MessagingConfiguration>()
    ?? new MessagingConfiguration();

// One line per event type - that's it!
services.AddEventQueueProcessor<YourDomainEvent, YourDomainEventHandler>(
    messagingConfig.SQS.YourDomainEventQueueUrl);

// The extension method wires up:
// - IEventQueue<YourDomainEvent> using SqsEventQueue<YourDomainEvent>
// - IEventHandler<YourDomainEvent> using YourDomainEventHandler
// - EventQueueProcessor<YourDomainEvent> as background service
```

### Step 4: Add Configuration

**appsettings.json:**
```json
{
  "MessagingConfiguration": {
    "SQS": {
      "YourDomainEventQueueUrl": "http://localhost:4566/000000000000/gearify-your-domain-event-queue"
    }
  }
}
```

**MessagingConfiguration.cs:**
```csharp
public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SqsConfiguration
{
    public string YourDomainEventQueueUrl { get; set; } = string.Empty;
    // One queue URL per event type you consume
}
```

### Step 5: Create Infrastructure (Queue + SNS Subscription)

Add to `localstack/scripts/init-sqs.sh`:
```bash
# YourDomainEvent -> Your Service
awslocal sqs create-queue \
  --queue-name gearify-your-domain-event-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$YOUR_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1
```

Add to `localstack/scripts/init-sns.sh`:
```bash
# Subscribe with filter policy
QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-your-domain-event-queue \
  --attribute-names QueueArn --region us-east-1 \
  --output text --query 'Attributes.QueueArn')

awslocal sns subscribe \
  --topic-arn $YOUR_TOPIC_ARN \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --attributes '{"FilterPolicy":"{\"EventType\":[\"YourDomainEvent\"]}"}' \
  --region us-east-1
```

---

## AWS Infrastructure Setup

### Pattern: One Queue Per Event Type

Every event type that a service consumes gets its own queue with an SNS filter policy.

### Creating SNS Topic

```bash
awslocal sns create-topic --name gearify-your-events --region us-east-1
# Returns: arn:aws:sns:us-east-1:000000000000:gearify-your-events
```

### Creating SQS Queue with DLQ

```bash
# Create DLQ first
awslocal sqs create-queue \
  --queue-name gearify-your-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1

# Get DLQ ARN
DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-your-events-dlq \
  --attribute-names QueueArn --region us-east-1 \
  --output text --query 'Attributes.QueueArn')

# Create queue with redrive policy
awslocal sqs create-queue \
  --queue-name gearify-your-domain-event-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1
```

### Creating SNS → SQS Subscription with Filter Policy

```bash
# Get the queue ARN
QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-your-domain-event-queue \
  --attribute-names QueueArn --region us-east-1 \
  --output text --query 'Attributes.QueueArn')

# Subscribe with filter policy (REQUIRED for one-queue-per-event pattern)
awslocal sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:000000000000:gearify-your-events \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --attributes '{"FilterPolicy":"{\"EventType\":[\"YourDomainEvent\"]}"}' \
  --region us-east-1
```

### Complete Example: Payment Events Fan-Out

```bash
#!/bin/bash
# Example: Setting up payment events with separate queues

PAYMENT_TOPIC_ARN="arn:aws:sns:us-east-1:000000000000:gearify-payment-events"
PAYMENT_DLQ_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-payment-events-dlq \
  --attribute-names QueueArn --region us-east-1 \
  --output text --query 'Attributes.QueueArn')

# PaymentCompletedEvent -> Order Service
awslocal sqs create-queue --queue-name gearify-payment-completed-queue \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"}" \
  --region us-east-1

QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-payment-completed-queue \
  --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn')

awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentCompletedEvent\"]}"}' \
  --region us-east-1

# PaymentFailedEvent -> Order Service
awslocal sqs create-queue --queue-name gearify-payment-failed-queue \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"}" \
  --region us-east-1

QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-payment-failed-queue \
  --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn')

awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentFailedEvent\"]}"}' \
  --region us-east-1

# RefundCompletedEvent -> Order Service
awslocal sqs create-queue --queue-name gearify-refund-completed-queue \
  --attributes "{\"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"}" \
  --region us-east-1

QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/gearify-refund-completed-queue \
  --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn')

awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs \
  --notification-endpoint $QUEUE_ARN \
  --attributes '{"FilterPolicy":"{\"EventType\":[\"RefundCompletedEvent\"]}"}' \
  --region us-east-1
```

---

## Error Handling & Retry Strategy

```
┌─────────────────────┐
│  IEventHandler      │
│  .HandleAsync()     │
└──────────┬──────────┘
           │
           ├─────────────────────────────────────────┐
           │                                         │
           ▼ return true                             ▼ return false / exception
┌─────────────────────┐                   ┌─────────────────────┐
│                     │                   │                     │
│  Delete from SQS    │                   │  Message stays in   │
│  (Success)          │                   │  queue              │
│                     │                   │                     │
└─────────────────────┘                   └──────────┬──────────┘
                                                     │
                                                     │ After visibility timeout
                                                     ▼
                                          ┌─────────────────────┐
                                          │  Retry (redelivered)│
                                          │                     │
                                          └──────────┬──────────┘
                                                     │
                                                     │ After maxReceiveCount
                                                     ▼
                                          ┌─────────────────────┐
                                          │  Dead Letter Queue  │
                                          │  (DLQ)              │
                                          └─────────────────────┘
```

**Retry Configuration (SQS):**
```bash
# Create DLQ
awslocal sqs create-queue --queue-name your-queue-dlq

# Set redrive policy on main queue
awslocal sqs set-queue-attributes \
  --queue-url http://localhost:4566/000000000000/your-queue \
  --attributes '{
    "RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:000000000000:your-queue-dlq\",\"maxReceiveCount\":\"3\"}"
  }'
```

---

## Service Folder Structure

```
gearify-{service}/
├── Domain/
│   └── Events/                         # Outbound domain events (published by this service)
│       ├── YourCreatedEvent.cs
│       └── YourUpdatedEvent.cs
│
└── Infrastructure/
    ├── Configuration/
    │   └── MessagingConfiguration.cs   # SNS/SQS config classes
    │
    └── Messaging/
        ├── SnsEventPublisher.cs        # Extends SnsEventPublisherBase
        │
        ├── Events/
        │   └── Inbound/                # One DTO per event type consumed
        │       ├── PaymentCompletedEvent.cs
        │       ├── PaymentFailedEvent.cs
        │       └── RefundCompletedEvent.cs
        │
        └── Handlers/                   # One handler per event type
            ├── PaymentCompletedEventHandler.cs
            ├── PaymentFailedEventHandler.cs
            └── RefundCompletedEventHandler.cs
```

### Naming Conventions

| Component | Pattern | Example |
|-----------|---------|---------|
| Inbound Event DTO | `{EventName}.cs` | `PaymentCompletedEvent.cs` |
| Handler | `{EventName}Handler.cs` | `PaymentCompletedEventHandler.cs` |
| Queue Name | `gearify-{event-name}-queue` | `gearify-payment-completed-queue` |
| Config Property | `{EventName}QueueUrl` | `PaymentCompletedQueueUrl` |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **One Queue Per Event Type** | Scalability (scale handlers independently), debuggability (easy tracing), clarity (handler name = purpose) |
| **SNS Filter Policies** | Event routing handled at infrastructure level, no code-level filtering needed |
| **EventEnvelope pattern** | Consistent metadata (eventId, eventType, tenantId, timestamp) across all services for routing and idempotency |
| **SnsEventPublisherBase** | Eliminates boilerplate; services only implement topic routing |
| **Generic SqsEventQueue\<T\>** | Single implementation handles envelope unwrapping and deserialization |
| **AddEventQueueProcessor extension** | One-line registration in Startup.cs reduces boilerplate |
| **One Handler Per Event Type** | Simple, focused handlers that do one thing; easy to understand and maintain |
| **Handler returns bool** | Clean contract: `true` = delete, `false` = retry |
| **Never throw on publish** | Event publishing failure shouldn't fail the main operation |
| **Scoped DI per polling cycle** | Ensures fresh DbContext and dependencies per batch |
| **Sequential message processing** | Simpler error handling; parallelism can be added per-handler if needed |
| **Reflection for TenantId extraction** | Convention over configuration; events with TenantId property are automatically supported |

---

## Troubleshooting

### Messages not being received

1. **Check queue URL configuration:**
   ```csharp
   _logger.LogInformation("Queue URL: {Url}", config.Value.SQS.YourDomainEventQueueUrl);
   ```

2. **Verify SNS subscription exists:**
   ```bash
   awslocal sns list-subscriptions-by-topic --topic-arn arn:aws:sns:us-east-1:000000000000:your-topic --region us-east-1
   ```

3. **Check SNS filter policy is set correctly:**
   ```bash
   # Get subscription ARN first
   awslocal sns list-subscriptions --region us-east-1

   # Then check its filter policy
   awslocal sns get-subscription-attributes \
     --subscription-arn arn:aws:sns:us-east-1:000000000000:your-topic:subscription-id \
     --region us-east-1 \
     --query 'Attributes.FilterPolicy'
   ```

4. **Verify EventType attribute is being published:**
   - Check that the publisher includes `EventType` as a message attribute
   - SNS filter policies filter on message attributes, not message body

### Messages going to wrong queue

1. **Check filter policy matches event type exactly:**
   ```bash
   # Filter policy should match the exact event type name
   {"EventType": ["PaymentCompletedEvent"]}  # Correct
   {"EventType": ["paymentcompleted"]}       # Wrong - case sensitive!
   ```

2. **Verify publisher is setting EventType attribute:**
   - `SnsEventPublisherBase` sets this automatically from the event class name

### Handler not being called

1. **Verify AddEventQueueProcessor was called:**
   ```csharp
   services.AddEventQueueProcessor<YourDomainEvent, YourDomainEventHandler>(
       messagingConfig.SQS.YourDomainEventQueueUrl);
   ```

2. **Check service is running (look for log output):**
   ```
   [INF] EventQueueProcessor<YourDomainEvent> starting...
   ```

### Payload deserialization issues

1. **Ensure DTO properties match domain event payload:**
   - Property names are case-insensitive by default
   - Check for missing or renamed properties

2. **Check logs for deserialization errors:**
   ```
   [ERR] Failed to deserialize message payload
   ```

### Duplicate message processing

1. **Implement idempotency in handler using EventId:**
   ```csharp
   // The EventEnvelope contains a unique EventId
   if (await _deduplicationService.HasProcessed(eventId))
       return true;  // Already processed, delete from queue
   ```

### Messages stuck in queue

1. **Check visibility timeout:**
   - Default is 300 seconds (5 minutes)
   - If handler takes longer, message becomes visible again

2. **Check DLQ for poison messages:**
   ```bash
   awslocal sqs get-queue-attributes \
     --queue-url http://localhost:4566/000000000000/your-events-dlq \
     --attribute-names ApproximateNumberOfMessages \
     --region us-east-1
   ```
