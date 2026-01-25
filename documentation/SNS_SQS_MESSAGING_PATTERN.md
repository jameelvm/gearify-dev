# SNS/SQS Messaging Pattern

## Overview

This document describes the standardized event-driven messaging pattern used across all Gearify microservices for asynchronous inter-service communication via AWS SNS and SQS.

All services use a consistent **EventEnvelope** pattern that wraps domain events with metadata for routing, filtering, and idempotency.

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
                                      │ Publish
                                      ▼
                         ┌────────────────────────┐
                         │                        │
                         │      AWS SNS Topic     │
                         │   (payment-events)     │
                         │                        │
                         └───────────┬────────────┘
                                     │
                    ┌────────────────┼────────────────┐
                    │ Subscribe      │ Subscribe      │ Subscribe (Fan-out)
                    ▼                ▼                ▼
           ┌────────────────┐ ┌────────────────┐ ┌────────────────┐
           │   SQS Queue    │ │   SQS Queue    │ │   SQS Queue    │
           │ (order-svc)    │ │ (notif-svc)    │ │ (future-svc)   │
           └───────┬────────┘ └───────┬────────┘ └───────┬────────┘
                   │                  │                  │
┌──────────────────┼──────────────────┼──────────────────┼─────────────────────────────────┐
│                  │                  │                  │           CONSUMER SIDE          │
│                  ▼                  ▼                  ▼                                  │
│  ┌───────────────────────────────────────────────────────────────────────────────────┐  │
│  │                        SqsEventQueue<T> (SharedKernel)                             │  │
│  │                                                                                    │  │
│  │  1. Long-poll SQS for messages                                                     │  │
│  │  2. Unwrap SNS envelope → EventEnvelope                                            │  │
│  │  3. Filter by EventType (optional)                                                 │  │
│  │  4. Deserialize Payload → T                                                        │  │
│  │  5. Enrich message with EventType (optional)                                       │  │
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
│  │  - Contains business logic                                                         │  │
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
    public SqsEventQueue(
        IAmazonSQS sqsClient,
        string queueUrl,
        ILogger<SqsEventQueue<T>> logger,
        IEnumerable<string>? eventTypeFilters = null,    // Filter by event type
        Func<T, string, T>? eventTypeEnricher = null);   // Enrich message with EventType

    public Task<List<QueueMessage<T>>> ReceiveMessagesAsync(...);
    public Task DeleteMessageAsync(string receiptHandle, ...);
}
```

**Parameters:**
| Parameter | Description |
|-----------|-------------|
| `queueUrl` | SQS queue URL to consume from |
| `eventTypeFilters` | Only process these event types (case-insensitive). Messages not matching are auto-deleted. |
| `eventTypeEnricher` | Function to set EventType on deserialized message, e.g., `(msg, type) => msg with { EventType = type }` |

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
│ 3. Check EventType Filter       │
│    If not in filter → DELETE    │
└─────────────────┬───────────────┘
                  │
                  ▼
┌─────────────────────────────────┐
│ 4. Deserialize Payload → T      │
└─────────────────┬───────────────┘
                  │
                  ▼
┌─────────────────────────────────┐
│ 5. Enrich with EventType        │
│    (if enricher provided)       │
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
│                                                                                               │
│  ┌─────────────┐         gearify-order-events          ┌─────────────────┐                   │
│  │   ORDER     │ ──────────────────────────────────────>│  SNS Topic      │                   │
│  │   SERVICE   │  (OrderCreatedEvent,                   │                 │                   │
│  │             │   OrderStatusChangedEvent)              └────────┬────────┘                   │
│  │             │                                                 │                            │
│  │             │◄─── gearify-payment-completed-queue ◄───┐       │                            │
│  └─────────────┘                                         │       ▼                            │
│                                                          │  gearify-order-created-queue       │
│                                                          │       │                            │
│                                                          │       ▼                            │
│  ┌─────────────┐         gearify-payment-events          │  ┌─────────────────┐              │
│  │  PAYMENT    │ ──────────────────────────────────────>│  │   PAYMENT       │              │
│  │  SERVICE    │  (PaymentCompletedEvent,               │  │   SERVICE       │              │
│  │             │   PaymentFailedEvent,                  │  │   (Consumer)    │              │
│  │             │   PaymentProcessingEvent)               │  └─────────────────┘              │
│  │             │◄─── gearify-order-created-queue ◄──────┘                                    │
│  └─────────────┘                                                                              │
│        │                                                                                      │
│        │ gearify-payment-events (Fan-out)                                                     │
│        │                                                                                      │
│        ├────────────────────────────────────────────────────┐                                 │
│        ▼                                                    ▼                                 │
│  gearify-payment-completed-queue                notification-payment-events-queue             │
│  (Order Service)                                (Notification Service)                        │
│                                                                                               │
│                                                 ┌─────────────────┐                          │
│                                                 │  NOTIFICATION   │                          │
│                                                 │  SERVICE        │                          │
│                                                 │  (Consumer only)│                          │
│                                                 └─────────────────┘                          │
│                                                                                               │
│                                                                                               │
│  ┌─────────────┐      gearify-media-upload-events       ┌─────────────────┐                  │
│  │   MEDIA     │ ─────────────────────────────────────> │  SNS Topic      │                  │
│  │   SERVICE   │  (MediaUploadedEvent)                  └────────┬────────┘                  │
│  │             │                                                 │                            │
│  │             │◄── gearify-image-processing-queue ◄─────────────┘ (self-subscribe)          │
│  │             │                                                                              │
│  │             │      gearify-image-processing-completed                                      │
│  │             │ ─────────────────────────────────────> ┌────────────────┐                    │
│  │             │  (ImageProcessingCompletedEvent)       │  SNS Topic     │                    │
│  └─────────────┘                                        └───────┬────────┘                    │
│                                                                 │                             │
│                                                                 ▼                             │
│                                        gearify-product-thumbnail-update-queue                 │
│                                                                 │                             │
│                                                                 ▼                             │
│  ┌─────────────┐       catalog-events-topic            ┌─────────────────┐                   │
│  │  CATALOG    │ ─────────────────────────────────────>│  SNS Topic      │                   │
│  │  SERVICE    │  (ProductCreatedEvent,                │                 │                   │
│  │             │   ProductUpdatedEvent,                └────────┬────────┘                   │
│  │             │   ProductDeletedEvent)                         │                            │
│  │             │                                                ▼                            │
│  │             │◄── gearify-product-thumbnail-          search-catalog-events-queue           │
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
│                                                                                               │
└──────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

### SNS Topics

| # | SNS Topic Name | Topic ARN | Publisher Service | Events Published |
|---|---------------|-----------|-------------------|------------------|
| 1 | `gearify-order-events` | `arn:aws:sns:us-east-1:000000000000:gearify-order-events` | **Order Service** | `OrderCreatedEvent`, `OrderStatusChangedEvent` |
| 2 | `gearify-payment-events` | `arn:aws:sns:us-east-1:000000000000:gearify-payment-events` | **Payment Service** | `PaymentCompletedEvent`, `PaymentFailedEvent`, `PaymentProcessingEvent` |
| 3 | `gearify-media-upload-events` | `arn:aws:sns:us-east-1:000000000000:gearify-media-upload-events` | **Media Service** | `MediaUploadedEvent` |
| 4 | `gearify-image-processing-completed` | `arn:aws:sns:us-east-1:000000000000:gearify-image-processing-completed` | **Media Service** | `ImageProcessingCompletedEvent` |
| 5 | `catalog-events-topic` | `arn:aws:sns:us-east-1:000000000000:catalog-events-topic` | **Catalog Service** | `ProductCreatedEvent`, `ProductUpdatedEvent`, `ProductDeletedEvent` |
| 6 | `gearify-shipping-events` | `arn:aws:sns:us-east-1:000000000000:gearify-shipping-events` | **Shipping Service** | `ShipmentCreated`, `ShipmentStatusUpdated`, `ShipmentDelivered` |
| 7 | `gearify-inventory-events` | `arn:aws:sns:us-east-1:000000000000:gearify-inventory-events` | **Inventory Service** | *(No subscribers yet)* |
| 8 | `gearify-checkout-events` | `arn:aws:sns:us-east-1:000000000000:gearify-checkout-events` | **Checkout Service** | *(No subscribers yet)* |

---

### SQS Queues & Subscriptions

| # | SQS Queue Name | Queue URL | Subscribes To (SNS Topic) | Consumer Service | Event Type Filter | Message Type |
|---|---------------|-----------|---------------------------|------------------|-------------------|--------------|
| 1 | `gearify-order-created-queue` | `http://localstack:4566/000000000000/gearify-order-created-queue` | `gearify-order-events` | **Payment Service** | `OrderCreatedEvent` | `OrderCreatedEventMessage` |
| 2 | `gearify-payment-completed-queue` | `http://localstack:4566/000000000000/gearify-payment-completed-queue` | `gearify-payment-events` | **Order Service** | `PaymentCompletedEvent`, `PaymentFailedEvent` | `PaymentEventMessage` |
| 3 | `notification-payment-events-queue` | `http://localstack:4566/000000000000/notification-payment-events-queue` | `gearify-payment-events` | **Notification Service** | `PaymentFailedEvent` | `PaymentFailedEventMessage` |
| 4 | `gearify-image-processing-queue` | `http://localstack:4566/000000000000/gearify-image-processing-queue` | `gearify-media-upload-events` | **Media Service** (self) | `MediaUploadedEvent` | `ImageProcessingEventMessage` |
| 5 | `gearify-product-thumbnail-update-queue` | `http://localstack:4566/000000000000/gearify-product-thumbnail-update-queue` | `gearify-image-processing-completed` | **Catalog Service** | `ImageProcessingCompletedEvent` | `ImageProcessingCompletedEventMessage` |
| 6 | `search-catalog-events-queue` | `http://localstack:4566/000000000000/search-catalog-events-queue` | `catalog-events-topic` | **Search Service** | `ProductCreatedEvent`, `ProductUpdatedEvent`, `ProductDeletedEvent` | `CatalogEventMessage` |
| 7 | `gearify-shipping-created-queue` | `http://localstack:4566/000000000000/gearify-shipping-created-queue` | `gearify-shipping-events` | *(TBD)* | `ShipmentCreated` | *(TBD)* |
| 8 | `gearify-shipping-status-queue` | `http://localstack:4566/000000000000/gearify-shipping-status-queue` | `gearify-shipping-events` | *(TBD)* | `ShipmentStatusUpdated`, `ShipmentDelivered` | *(TBD)* |

---

### Fan-Out: Topics With Multiple Subscribers

The following topics deliver to more than one queue:

**`gearify-payment-events`** (2 subscribers)

```
Payment Service
      │
      │ publishes PaymentCompletedEvent / PaymentFailedEvent
      ▼
┌──────────────────────────┐
│ gearify-payment-events   │
│ (SNS Topic)              │
└─────────────┬────────────┘
              │
     ┌────────┴──────────────────────────┐
     │                                   │
     ▼                                   ▼
┌──────────────────────────┐   ┌──────────────────────────────────┐
│gearify-payment-completed │   │notification-payment-events-queue │
│-queue                    │   │                                  │
│                          │   │                                  │
│ → Order Service          │   │ → Notification Service           │
│   Filters: Completed,    │   │   Filters: FailedEvent only     │
│            FailedEvent   │   │                                  │
│   Handles: Update order  │   │   Handles: Send failure email   │
│            status        │   │            to customer           │
└──────────────────────────┘   └──────────────────────────────────┘
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
│ Filters: ShipmentCreated │   │ Filters: ShipmentStatusUpdated,  │
│                          │   │          ShipmentDelivered        │
└──────────────────────────┘   └──────────────────────────────────┘
```

---

### Dead Letter Queues (DLQs)

| DLQ Name | Protects Queue | Max Receive Count |
|----------|----------------|-------------------|
| `gearify-checkout-events-dlq` | checkout event queues | 3 |
| `gearify-order-events-dlq` | order event queues | 3 |
| `gearify-payment-events-dlq` | payment event queues | 3 |
| `gearify-shipping-events-dlq` | shipping event queues | 3 |

---

### Quick Reference: "Where does this event go?"

| Event | Published By | SNS Topic | Consumed By (Queue) |
|-------|-------------|-----------|---------------------|
| `OrderCreatedEvent` | Order Service | `gearify-order-events` | Payment Service (`gearify-order-created-queue`) |
| `OrderStatusChangedEvent` | Order Service | `gearify-order-events` | *(no consumer yet)* |
| `PaymentCompletedEvent` | Payment Service | `gearify-payment-events` | Order Service (`gearify-payment-completed-queue`) |
| `PaymentFailedEvent` | Payment Service | `gearify-payment-events` | Order Service (`gearify-payment-completed-queue`), Notification Service (`notification-payment-events-queue`) |
| `PaymentProcessingEvent` | Payment Service | `gearify-payment-events` | *(no consumer yet)* |
| `MediaUploadedEvent` | Media Service | `gearify-media-upload-events` | Media Service (`gearify-image-processing-queue`) |
| `ImageProcessingCompletedEvent` | Media Service | `gearify-image-processing-completed` | Catalog Service (`gearify-product-thumbnail-update-queue`) |
| `ProductCreatedEvent` | Catalog Service | `catalog-events-topic` | Search Service (`search-catalog-events-queue`) |
| `ProductUpdatedEvent` | Catalog Service | `catalog-events-topic` | Search Service (`search-catalog-events-queue`) |
| `ProductDeletedEvent` | Catalog Service | `catalog-events-topic` | Search Service (`search-catalog-events-queue`) |
| `ShipmentCreated` | Shipping Service | `gearify-shipping-events` | *(TBD)* (`gearify-shipping-created-queue`) |
| `ShipmentStatusUpdated` | Shipping Service | `gearify-shipping-events` | *(TBD)* (`gearify-shipping-status-queue`) |
| `ShipmentDelivered` | Shipping Service | `gearify-shipping-events` | *(TBD)* (`gearify-shipping-status-queue`) |

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

### Step 1: Define the Inbound Message Model

**Location:** `{service}/Infrastructure/Messaging/Events/Inbound/`

```csharp
namespace YourService.Infrastructure.Messaging.Events.Inbound;

public record YourEventMessage
{
    // EventType is extracted from the envelope
    public string EventType { get; init; } = string.Empty;

    // Fields matching the domain event's payload
    public string TenantId { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string SomeData { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}
```

### Step 2: Implement the Event Handler

**Location:** `{service}/Infrastructure/Messaging/YourEventHandler.cs`

```csharp
using Gearify.SharedKernel.Messaging;
using MediatR;

namespace YourService.Infrastructure.Messaging;

public class YourEventHandler : IEventHandler<YourEventMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<YourEventHandler> _logger;

    public YourEventHandler(IMediator mediator, ILogger<YourEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(YourEventMessage message, CancellationToken ct)
    {
        _logger.LogInformation("Processing {EventType} for entity {EntityId}",
            message.EventType, message.EntityId);

        try
        {
            // Dispatch to appropriate command based on event type
            var command = new ProcessYourEventCommand(
                message.EntityId,
                message.SomeData,
                message.TenantId);

            await _mediator.Send(command, ct);

            return true;  // Success - delete from queue
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {EventType}", message.EventType);
            return false;  // Failure - keep in queue for retry
        }
    }
}
```

### Step 3: Register in Startup.cs

```csharp
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// In ConfigureServices():

// SQS Client (if not already registered)
services.AddSingleton<IAmazonSQS>(sp =>
{
    var config = new AmazonSQSConfig
    {
        ServiceURL = Environment.GetEnvironmentVariable("SQS_ENDPOINT") ?? "http://localhost:4566"
    };
    return new AmazonSQSClient(config);
});

// Event Queue (using generic SqsEventQueue)
services.AddScoped<IEventQueue<YourEventMessage>>(sp =>
{
    var sqsClient = sp.GetRequiredService<IAmazonSQS>();
    var config = sp.GetRequiredService<IOptions<MessagingConfiguration>>();
    var logger = sp.GetRequiredService<ILogger<SqsEventQueue<YourEventMessage>>>();

    return new SqsEventQueue<YourEventMessage>(
        sqsClient,
        config.Value.SQS.YourQueueUrl,
        logger,
        eventTypeFilters: ["YourDomainEvent"],  // Only process these event types
        eventTypeEnricher: (msg, type) => msg with { EventType = type });
});

// Event Handler
services.AddScoped<IEventHandler<YourEventMessage>, YourEventHandler>();

// Background Processor
services.AddHostedService<EventQueueProcessor<YourEventMessage>>();
```

### Step 4: Add Configuration

**appsettings.json:**
```json
{
  "MessagingConfiguration": {
    "SQS": {
      "YourQueueUrl": "http://localhost:4566/000000000000/your-queue"
    }
  }
}
```

**MessagingConfiguration.cs:**
```csharp
public class MessagingConfiguration
{
    public SqsConfiguration SQS { get; set; } = new();
}

public class SqsConfiguration
{
    public string YourQueueUrl { get; set; } = string.Empty;
}
```

---

## AWS Infrastructure Setup

### Creating SNS Topic

**LocalStack (docker-compose or init script):**
```bash
awslocal sns create-topic --name your-events
# Returns: arn:aws:sns:us-east-1:000000000000:your-events
```

### Creating SQS Queue

```bash
awslocal sqs create-queue --queue-name your-service-your-events
# Returns: http://localhost:4566/000000000000/your-service-your-events
```

### Creating SNS → SQS Subscription

```bash
# Get the queue ARN
awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/your-service-your-events \
  --attribute-names QueueArn

# Subscribe queue to topic
awslocal sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:000000000000:your-events \
  --protocol sqs \
  --notification-endpoint arn:aws:sqs:us-east-1:000000000000:your-service-your-events
```

### Adding Event Type Filter Policy (Optional)

Filter at SNS level to only deliver specific event types to a queue:

```bash
awslocal sns set-subscription-attributes \
  --subscription-arn arn:aws:sns:us-east-1:000000000000:your-events:subscription-id \
  --attribute-name FilterPolicy \
  --attribute-value '{"EventType": ["YourDomainEvent", "AnotherEvent"]}'
```

### Complete Infrastructure Script Example

```bash
#!/bin/bash
# init-messaging.sh

# Create Payment Events Topic
awslocal sns create-topic --name payment-events

# Create queues for subscribers
awslocal sqs create-queue --queue-name order-payment-events
awslocal sqs create-queue --queue-name notification-payment-events

# Subscribe Order Service to payment events (all events)
QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/order-payment-events \
  --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

awslocal sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:000000000000:payment-events \
  --protocol sqs \
  --notification-endpoint $QUEUE_ARN

# Subscribe Notification Service (only PaymentFailedEvent)
NOTIF_QUEUE_ARN=$(awslocal sqs get-queue-attributes \
  --queue-url http://localhost:4566/000000000000/notification-payment-events \
  --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

SUB_ARN=$(awslocal sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:000000000000:payment-events \
  --protocol sqs \
  --notification-endpoint $NOTIF_QUEUE_ARN \
  --query 'SubscriptionArn' --output text)

# Apply filter policy
awslocal sns set-subscription-attributes \
  --subscription-arn $SUB_ARN \
  --attribute-name FilterPolicy \
  --attribute-value '{"EventType": ["PaymentFailedEvent"]}'
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
│   └── Events/                         # Outbound domain events
│       ├── YourCreatedEvent.cs
│       └── YourUpdatedEvent.cs
│
└── Infrastructure/
    ├── Configuration/
    │   └── MessagingConfiguration.cs   # SNS/SQS config classes
    │
    └── Messaging/
        ├── SnsEventPublisher.cs        # Extends SnsEventPublisherBase
        ├── YourEventHandler.cs         # IEventHandler<T> implementation
        │
        └── Events/
            └── Inbound/
                └── YourEventMessage.cs # Deserialization model for incoming events
```

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| **EventEnvelope pattern** | Consistent metadata (eventId, eventType, tenantId, timestamp) across all services for filtering, routing, and idempotency |
| **SnsEventPublisherBase** | Eliminates boilerplate; services only implement topic routing |
| **Generic SqsEventQueue\<T\>** | Single implementation handles envelope unwrapping, filtering, enrichment |
| **EventType filtering in SqsEventQueue** | Application-level filtering; more flexible than SNS filter policies |
| **EventType enricher function** | Allows handlers to know which event type they're processing without reflection |
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
   _logger.LogInformation("Queue URL: {Url}", config.Value.SQS.YourQueueUrl);
   ```

2. **Verify SNS subscription exists:**
   ```bash
   awslocal sns list-subscriptions-by-topic --topic-arn arn:aws:sns:...
   ```

3. **Check filter policy (if using SNS filtering):**
   ```bash
   awslocal sns get-subscription-attributes --subscription-arn arn:aws:sns:...
   ```

### Messages being deleted without processing

1. **Check event type filter:**
   - If `eventTypeFilters` is set, messages not matching are auto-deleted
   - Log shows: `"Message filtered out: EventType X not in filter list"`

2. **Check payload deserialization:**
   - Ensure message model properties match the domain event payload
   - Use `PropertyNameCaseInsensitive = true` in JSON options

### Handler not being called

1. **Verify DI registration:**
   ```csharp
   services.AddScoped<IEventHandler<YourMessage>, YourHandler>();
   ```

2. **Check background service registration:**
   ```csharp
   services.AddHostedService<EventQueueProcessor<YourMessage>>();
   ```

### Duplicate message processing

1. **Implement idempotency in handler:**
   ```csharp
   // Use EventId from envelope for deduplication
   if (await _deduplicationService.HasProcessed(message.EventId))
       return true;  // Already processed, delete from queue
   ```
