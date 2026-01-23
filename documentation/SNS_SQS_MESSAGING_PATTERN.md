# SNS/SQS Messaging Pattern

## Architecture Overview

This document describes the generic event-driven messaging pattern used across Gearify microservices for asynchronous inter-service communication via AWS SNS and SQS.

---

## High-Level Architecture

```
+------------------+       SNS Topic        +------------------+
|                  |  (Publish)             |                  |
|  Producer        |----------------------->|  AWS SNS         |
|  Service         |                        |  (Fan-out)       |
|                  |                        |                  |
+------------------+                        +--------+---------+
                                                     |
                                          SNS Subscription
                                                     |
                                            +--------v---------+
                                            |                  |
                                            |  AWS SQS Queue   |
                                            |  (Per Consumer)  |
                                            |                  |
                                            +--------+---------+
                                                     |
                                              Long Polling
                                                     |
                                            +--------v---------+
                                            |                  |
                                            |  Consumer        |
                                            |  Service         |
                                            |                  |
                                            +------------------+
```

---

## Generic Messaging Components (SharedKernel)

The pattern is implemented with three generic components in `Gearify.SharedKernel.Messaging`:

```
+-------------------------------------------------------------+
|                     SharedKernel.Messaging                    |
|                                                              |
|  +-------------------+  +--------------------+               |
|  | IEventQueue<T>    |  | IEventHandler<T>   |               |
|  |                   |  |                    |               |
|  | ReceiveMessages() |  | HandleAsync(T msg) |               |
|  | DeleteMessage()   |  | returns: bool      |               |
|  | ReturnMessage()   |  | (true = delete)    |               |
|  +-------------------+  +--------------------+               |
|           ^                       ^                          |
|           |                       |                          |
|  +--------+----------+            |                          |
|  | QueueMessage<T>   |  +---------+-----------+              |
|  |                   |  | EventQueueProcessor |              |
|  | MessageId         |  | <T>                 |              |
|  | ReceiptHandle     |  |                     |              |
|  | Body: T           |  | BackgroundService   |              |
|  +-------------------+  | Polls IEventQueue   |              |
|                         | Delegates to        |              |
|                         | IEventHandler       |              |
|                         +---------------------+              |
+-------------------------------------------------------------+
```

---

## Message Flow Sequence

```
┌──────────────┐     ┌─────────┐     ┌─────────┐     ┌────────────────────────┐     ┌───────────────┐     ┌──────────────┐
│   Producer   │     │   SNS   │     │   SQS   │     │ EventQueueProcessor<T> │     │IEventHandler  │     │   MediatR    │
│   Service    │     │  Topic  │     │  Queue  │     │   (BackgroundService)  │     │<T>            │     │  (Commands)  │
└──────┬───────┘     └────┬────┘     └────┬────┘     └───────────┬────────────┘     └───────┬───────┘     └──────┬───────┘
       │                   │               │                      │                          │                    │
       │  Publish Event    │               │                      │                          │                    │
       │──────────────────>│               │                      │                          │                    │
       │                   │  Deliver Msg  │                      │                          │                    │
       │                   │──────────────>│                      │                          │                    │
       │                   │               │   Long Poll (20s)    │                          │                    │
       │                   │               │<─────────────────────│                          │                    │
       │                   │               │   Return Messages    │                          │                    │
       │                   │               │─────────────────────>│                          │                    │
       │                   │               │                      │                          │                    │
       │                   │               │                      │  HandleAsync(message)    │                    │
       │                   │               │                      │─────────────────────────>│                    │
       │                   │               │                      │                          │                    │
       │                   │               │                      │                          │  Send(Command)     │
       │                   │               │                      │                          │───────────────────>│
       │                   │               │                      │                          │                    │
       │                   │               │                      │                          │  Result            │
       │                   │               │                      │                          │<───────────────────│
       │                   │               │                      │                          │                    │
       │                   │               │                      │  return true (delete)    │                    │
       │                   │               │                      │<─────────────────────────│                    │
       │                   │               │                      │                          │                    │
       │                   │               │  DeleteMessage       │                          │                    │
       │                   │               │<─────────────────────│                          │                    │
       │                   │               │                      │                          │                    │
```

---

## Component Details

### 1. `IEventQueue<T>` (Interface)

Abstracts the queue transport (SQS). Each service provides a concrete implementation.

```csharp
public interface IEventQueue<T>
{
    Task<List<QueueMessage<T>>> ReceiveMessagesAsync(
        int maxMessages,
        int waitTimeSeconds,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default);

    Task ReturnMessageAsync(string receiptHandle, int visibilityTimeoutSeconds = 30,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
```

**Key points:**
- `ReceiveMessagesAsync` — Long-polls SQS for messages (batched)
- `DeleteMessageAsync` — Removes a successfully processed message from the queue
- `ReturnMessageAsync` — Returns a message to the queue for retry (default: not implemented, opt-in per service)

---

### 2. `IEventHandler<T>` (Interface)

Contains the business logic for processing a single event message.

```csharp
public interface IEventHandler<T>
{
    Task<bool> HandleAsync(T message, CancellationToken cancellationToken = default);
}
```

**Return value:**
- `true` — Message processed successfully; delete from queue
- `false` — Message should remain in queue for retry

---

### 3. `EventQueueProcessor<T>` (BackgroundService)

Generic polling loop that ties `IEventQueue<T>` and `IEventHandler<T>` together.

```csharp
public class EventQueueProcessor<T> : BackgroundService
```

**Behavior:**
- Runs as a hosted background service
- Creates a DI scope per polling cycle
- Resolves `IEventQueue<T>` and `IEventHandler<T>` from the scoped container
- Polls for up to 10 messages with 20-second long polling
- Processes messages sequentially within a batch
- Deletes messages when handler returns `true`
- On unhandled exceptions: logs error, waits 30 seconds, then resumes polling
- Messages that throw are left in the queue for SQS retry/DLQ

---

### 4. `QueueMessage<T>` (DTO)

Wraps a deserialized message body with SQS metadata needed for acknowledgement.

```csharp
public class QueueMessage<T>
{
    public string MessageId { get; set; }
    public string ReceiptHandle { get; set; }
    public T Body { get; set; }
}
```

---

## SNS Message Envelope

Messages published via SNS are wrapped in a JSON envelope when delivered to SQS:

```json
{
  "Message": "{\"EventId\":\"...\",\"EventType\":\"PaymentCompletedEvent\",\"TenantId\":\"...\",\"Timestamp\":\"...\",\"Payload\":{...}}",
  "MessageId": "sns-message-id",
  "TopicArn": "arn:aws:sns:region:account:topic-name"
}
```

The SQS queue implementation is responsible for:
1. Unwrapping the SNS envelope
2. Parsing the inner event envelope (EventId, EventType, TenantId, Timestamp, Payload)
3. Deserializing the Payload into the typed event message (`T`)

---

## Service Registration Pattern

To wire up a new event consumer, register three components in `Startup.cs`:

```csharp
// 1. Queue adapter (SQS implementation)
services.AddScoped<IEventQueue<TEventMessage>, SqsTEventQueue>();

// 2. Event handler (business logic)
services.AddScoped<IEventHandler<TEventMessage>, TEventHandler>();

// 3. Background processor (generic, from SharedKernel)
services.AddHostedService<EventQueueProcessor<TEventMessage>>();
```

---

## Current Implementations

| Service        | Event Message                        | Queue Implementation               | Handler                                | Source Event                |
|----------------|--------------------------------------|-------------------------------------|----------------------------------------|-----------------------------|
| Order Service  | `PaymentEventMessage`                | `SqsPaymentEventQueue`             | `PaymentEventHandler`                  | Payment Service             |
| Payment Service| `OrderCreatedEventMessage`           | `SqsOrderEventQueue`               | `OrderCreatedEventHandler`             | Order Service               |
| Catalog Service| `ImageProcessingCompletedEventMessage`| `SqsProductThumbnailUpdateQueue`   | `ImageProcessingCompletedEventHandler` | Media Service               |
| Media Service  | `ImageProcessingEventMessage`        | `SqsImageProcessingQueue`          | `ImageProcessingEventHandler`          | Catalog Service             |

---

## Adding a New Event Consumer

To consume a new event type in a service:

### Step 1: Define the inbound event model

```
Infrastructure/Messaging/Events/Inbound/NewEventMessage.cs
```

```csharp
namespace YourService.Infrastructure.Messaging.Events.Inbound;

public record NewEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    // ... event-specific fields
}
```

### Step 2: Implement the SQS queue adapter

```
Infrastructure/Messaging/SqsNewEventQueue.cs
```

Implement `IEventQueue<NewEventMessage>` with:
- SNS envelope unwrapping
- Event deserialization
- SQS message deletion

### Step 3: Implement the event handler

```
Infrastructure/Messaging/NewEventHandler.cs
```

```csharp
public class NewEventHandler : IEventHandler<NewEventMessage>
{
    public async Task<bool> HandleAsync(NewEventMessage msg, CancellationToken ct)
    {
        // Business logic here (typically sends a MediatR command)
        return true; // delete from queue
    }
}
```

### Step 4: Register in Startup.cs

```csharp
services.AddScoped<IEventQueue<NewEventMessage>, SqsNewEventQueue>();
services.AddScoped<IEventHandler<NewEventMessage>, NewEventHandler>();
services.AddHostedService<EventQueueProcessor<NewEventMessage>>();
```

---

## Error Handling & Retry Strategy

```
+-------------------+     Success      +------------------+
|                   |  (return true)   |                  |
|  IEventHandler    |─────────────────>|  Delete from     |
|  .HandleAsync()   |                  |  SQS Queue       |
|                   |                  |                  |
+--------+----------+                  +------------------+
         |
         | Failure (return false)
         v
+-------------------+
|                   |
|  Message stays    |
|  in queue         |
|  (visibility      |
|   timeout reset)  |
|                   |
+--------+----------+
         |
         | After max receives
         v
+-------------------+
|                   |
|  Dead Letter      |
|  Queue (DLQ)      |
|                   |
+-------------------+
```

**Strategy:**
- Handler returns `true` → message deleted (success)
- Handler returns `false` → message stays in queue, retried after visibility timeout
- Handler throws exception → caught by processor, message stays for retry, error logged
- After max receive count → SQS moves message to Dead Letter Queue (configured on the queue)

---

## Publishing Events (Outbound)

For the producer side, services use `ISnsEventPublisher` from SharedKernel:

```csharp
public interface ISnsEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent;
}
```

Events are published to an SNS topic, which fans out to subscribed SQS queues:

```
+------------------+        +------------------+       +------------------+
|  Payment Service |        |                  |       |  Order Service   |
|                  |Publish |  SNS Topic       | SQS   |  (Consumer)      |
|  PaymentCompleted|------->|  payment-events  |------>|                  |
|  Event           |        |                  |       |  PaymentEvent    |
|                  |        +--------+---------+       |  Handler         |
+------------------+                 |                 +------------------+
                                     |
                                     | (Future consumers
                                     |  can subscribe)
                                     v
                              +------------------+
                              |  Another Service |
                              |  (Fan-out)       |
                              +------------------+
```

---

## Folder Structure

```
gearify-shared-kernel/
└── Messaging/
    ├── IEventQueue.cs              # Generic queue interface
    ├── IEventHandler.cs            # Generic handler interface
    ├── EventQueueProcessor.cs      # Generic background processor
    └── QueueMessage.cs             # Message wrapper DTO

gearify-{service}/
├── Events/                         # Outbound domain events
│   ├── PaymentCompletedEvent.cs
│   └── PaymentFailedEvent.cs
└── Infrastructure/
    └── Messaging/
        ├── SnsEventPublisher.cs    # Publishes to SNS
        ├── Sqs{Name}Queue.cs       # IEventQueue<T> impl
        ├── {Name}EventHandler.cs   # IEventHandler<T> impl
        └── Events/
            └── Inbound/
                └── {Name}EventMessage.cs  # Deserialization model
```

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Generic `EventQueueProcessor<T>` | Eliminates boilerplate; each new consumer only needs a handler |
| Scoped DI per polling cycle | Ensures fresh DbContext and transient dependencies per batch |
| Sequential message processing | Simpler error handling; parallelism can be added per-handler if needed |
| Handler returns `bool` | Clean contract: `true` = processed, `false` = retry |
| `ReturnMessageAsync` as default interface method | Not all queues need explicit return; avoids forcing `NotImplementedException` in implementations |
| SNS envelope unwrapping in queue adapter | Keeps handler focused on business logic, unaware of transport details |
| Inbound models separate from domain events | Clean Architecture: Infrastructure models don't leak into Domain/Application |
