# Integration & Contract Tests

Automated tests that verify the full Order → Payment → Order event flow works end-to-end. Each piece of the messaging infrastructure (outbox, SNS, SQS, deserialization, idempotency, CorrelationId) is tested individually and as a connected chain.

## Test Projects

### 1. Gearify.SharedKernel.IntegrationTests

**Location:** `gearify-shared-kernel/Tests/Gearify.SharedKernel.IntegrationTests/`

Tests the shared messaging infrastructure in isolation using real containers (LocalStack, PostgreSQL, Redis) via [Testcontainers](https://dotnet.testcontainers.org/).

| Test Class | Type | Containers | Tests |
|---|---|---|---|
| `EventEnvelopeSerializationTests` | Unit / Contract | None | 5 |
| `OutboxMessageFactoryTests` | Unit / Contract | None | 4 |
| `SqsEventQueueDeserializationTests` | Integration | LocalStack | 4 |
| `OutboxPublisherIntegrationTests` | Integration | LocalStack + PostgreSQL | 3 |
| `RedisIdempotencyStoreTests` | Integration | Redis | 5 |
| `OutboxToSqsEndToEndTests` | End-to-End | LocalStack + PostgreSQL | 1 |

**Total: 22 tests**

### 2. Gearify.Messaging.IntegrationTests

**Location:** `tests/Gearify.Messaging.IntegrationTests/`

Cross-service contract tests that verify publisher event types can be deserialized into consumer event types across service boundaries. No containers needed — pure serialization round-trip tests.

| Test Class | Type | Tests |
|---|---|---|
| `CrossServiceEventContractTests` | Contract | 4 |

**Total: 4 tests**

---

## Prerequisites

- **.NET 8 SDK** (or later)
- **Docker Desktop** running (required for integration tests that use Testcontainers)
  - LocalStack, PostgreSQL, and Redis containers are started automatically
  - No manual Docker setup needed — Testcontainers handles container lifecycle

---

## How to Run

### Run all tests (both projects)

```bash
dotnet test gearify-shared-kernel/Tests/Gearify.SharedKernel.IntegrationTests/Gearify.SharedKernel.IntegrationTests.csproj
dotnet test tests/Gearify.Messaging.IntegrationTests/Gearify.Messaging.IntegrationTests.csproj
```

### Run only the fast unit/contract tests (no Docker needed)

```bash
dotnet test gearify-shared-kernel/Tests/Gearify.SharedKernel.IntegrationTests/Gearify.SharedKernel.IntegrationTests.csproj \
  --filter "FullyQualifiedName~EventEnvelopeSerializationTests|FullyQualifiedName~OutboxMessageFactoryTests"
```

### Run only cross-service contract tests (no Docker needed)

```bash
dotnet test tests/Gearify.Messaging.IntegrationTests/Gearify.Messaging.IntegrationTests.csproj
```

### Run only integration tests (Docker required)

```bash
dotnet test gearify-shared-kernel/Tests/Gearify.SharedKernel.IntegrationTests/Gearify.SharedKernel.IntegrationTests.csproj \
  --filter "FullyQualifiedName~SqsEventQueue|FullyQualifiedName~OutboxPublisher|FullyQualifiedName~RedisIdempotency|FullyQualifiedName~OutboxToSqs"
```

### Run a single test class

```bash
dotnet test gearify-shared-kernel/Tests/Gearify.SharedKernel.IntegrationTests/Gearify.SharedKernel.IntegrationTests.csproj \
  --filter "FullyQualifiedName~OutboxToSqsEndToEndTests"
```

---

## What Each Test Verifies

### EventEnvelope Serialization (Piece 2)

Pure unit tests verifying the `EventEnvelope.Wrap()` method and JSON round-trip behavior.

| Test | What it proves |
|---|---|
| `Wrap_SetsEventType_ToTypeName` | EventType is set to the CLR type name |
| `Wrap_SetsCorrelationId_WhenProvided` | CorrelationId passes through to envelope |
| `Wrap_GeneratesUniqueEventId` | Each wrap produces a distinct EventId |
| `RoundTrip_PreservesAllFields` | Serialize with camelCase → deserialize case-insensitive → all fields intact |
| `RoundTrip_NestedPayload_CanBeReDeserialized` | Payload survives double-deserialization (envelope → payload JSON → typed object) |

### OutboxMessageFactory (Piece 3)

Tests that `OutboxMessageFactory` correctly creates `OutboxMessage` records from domain events.

| Test | What it proves |
|---|---|
| `CreateOutboxMessage_SetsEventType_And_TopicArn` | EventType and TopicArn are resolved and set |
| `CreateOutboxMessage_SerializesPayloadAsEventEnvelope` | Payload JSON contains full EventEnvelope structure |
| `CreateOutboxMessage_PreservesCorrelationContext` | Current `CorrelationContext` value is captured in envelope |
| `CreateOutboxMessage_ThrowsWhenNoTopicArn` | Throws `InvalidOperationException` when topic ARN is not configured |

### SqsEventQueue Deserialization (Piece 4)

Integration tests using a real LocalStack container to verify the SQS consumer correctly unwraps SNS message envelopes.

| Test | What it proves |
|---|---|
| `ReceiveMessages_UnwrapsSnsEnvelope_DeserializesPayload` | SNS envelope → EventEnvelope → typed payload deserialization works |
| `ReceiveMessages_ExtractsEventId_And_CorrelationId` | EventId and CorrelationId are extracted from the envelope into `QueueMessage` |
| `ReceiveMessages_WithFilter_SkipsNonMatchingEventTypes` | Event type filtering only returns matching events |
| `DeleteMessage_RemovesFromQueue` | Deleting a message prevents it from being received again |

### OutboxPublisher (Piece 5)

Integration tests with real PostgreSQL and LocalStack containers verifying the background publisher.

| Test | What it proves |
|---|---|
| `PublishesToSns_And_MarksAsPublished` | Outbox message appears in SQS + `PublishedAt` is set in the database |
| `SkipsAlreadyPublished` | Messages with `PublishedAt` already set are not re-published |
| `RetriesOnFailure_WithExponentialBackoff` | Failed publishes increment `RetryCount` and set `NextRetryAt` |

### Redis Idempotency (Piece 6)

Integration tests with a real Redis container verifying atomic claim/release operations.

| Test | What it proves |
|---|---|
| `TryClaimEvent_FirstCall_ReturnsTrue` | First claim on a new event ID succeeds |
| `TryClaimEvent_SecondCall_ReturnsFalse` | Second claim on the same event ID is rejected (duplicate prevention) |
| `ReleaseClaim_AllowsReclaim` | Releasing a claim allows another consumer to process it |
| `MarkAsProcessed_MakesHasBeenProcessedTrue` | `HasBeenProcessedAsync` returns true after `MarkAsProcessedAsync` |
| `MarkAsProcessed_PreventsClaimAfter` | Cannot claim an already-processed event |

### End-to-End Flow (Piece 6)

A single test that exercises the full message chain.

| Test | What it proves |
|---|---|
| `FullFlow_OutboxFactory_ToPublisher_ToSns_ToSqs_PreservesCorrelationAndEventId` | `OutboxMessageFactory` → PostgreSQL → `OutboxPublisher` → SNS → SQS → `SqsEventQueue` — CorrelationId, EventId, and payload data survive the entire journey |

### Cross-Service Event Contracts (Piece 7)

Contract tests that catch serialization mismatches between publisher and consumer event types across service boundaries. These serialize a full publisher event through `EventEnvelope.Wrap()` with camelCase options, then deserialize into the consumer's inbound DTO with case-insensitive options — exactly mirroring the production SNS → SQS path.

| Test | Publisher → Consumer |
|---|---|
| `OrderCreatedEvent_Publisher_CompatibleWith_Consumer` | `OrderService.Events.OrderCreatedEvent` → `PaymentService.Infrastructure.Messaging.Events.Inbound.OrderCreatedEvent` |
| `PaymentCompletedEvent_Publisher_CompatibleWith_Consumer` | `PaymentService.Events.PaymentCompletedEvent` → `OrderService.Infrastructure.Messaging.Events.Inbound.PaymentCompletedEvent` |
| `PaymentFailedEvent_Publisher_CompatibleWith_Consumer` | `PaymentService.Events.PaymentFailedEvent` → `OrderService.Infrastructure.Messaging.Events.Inbound.PaymentFailedEvent` |
| `OrderCancelledEvent_Publisher_CompatibleWith_Consumer` | `OrderService.Events.OrderCancelledEvent` → `PaymentService.Infrastructure.Messaging.Events.Inbound.OrderCancelledEvent` |

---

## Test Fixtures (Shared Infrastructure)

All container-based tests share fixtures via XUnit collection definitions to avoid spinning up redundant containers.

| Fixture | Container | Shared via Collection |
|---|---|---|
| `LocalStackFixture` | `localstack/localstack:3.0` | `[Collection("LocalStack")]` |
| `PostgresFixture` | `postgres:16-alpine` | `[Collection("Postgres")]` |
| `RedisFixture` | `redis:7-alpine` | `[Collection("Redis")]` |
| `MessagingInfrastructureFixture` | All three in parallel | `[Collection("MessagingInfrastructure")]` |

The `LocalStackFixture` provides a `SetupTopicWithQueue(topicName, queueName, eventTypeFilter?)` helper that creates an SNS topic, SQS queue, and subscription with optional filter policy in a single call.

---

## Architecture: What These Tests Protect Against

```
Order Service                          Payment Service
┌─────────────┐                       ┌──────────────┐
│ OrderCreated │──┐                   │              │
│    Event     │  │   ┌───────────┐   │  Inbound DTO │
│ (full model) │  ├──>│ Outbox DB │   │  (subset)    │
└─────────────┘  │   └─────┬─────┘   └──────┬───────┘
                 │         │                 │
  Contract Test  │   ┌─────▼─────┐    ┌─────▼──────┐
  catches here ──┤   │ Publisher │    │ SqsEvent   │
                 │   │ (SNS)     │    │ Queue      │
                 │   └─────┬─────┘    └─────┬──────┘
                 │         │                 │
                 │   ┌─────▼─────────────────▼──┐
                 └──>│     SNS → SQS Flow       │
                     │  (EventEnvelope wrapper)  │
                     └──────────────────────────┘
```

**Without these tests, the following bugs would only surface in production:**
- A renamed property on the publisher event breaks consumer deserialization
- A new required field on the consumer DTO that the publisher doesn't send
- Enum-to-string conversion differences between publisher and consumer
- camelCase vs PascalCase serialization mismatches
- CorrelationId lost during the outbox → SNS → SQS chain
- EventId not surviving the SNS envelope wrapping/unwrapping
- Outbox publisher not marking messages as published after SNS success
- Redis idempotency store allowing duplicate event processing

---

## Approximate Run Times

| Scope | Time |
|---|---|
| Unit/contract tests only (9 tests, no Docker) | ~3 seconds |
| Cross-service contract tests (4 tests, no Docker) | ~3 seconds |
| Full suite including containers (26 tests) | ~2-3 minutes |

The first run may take longer if Docker needs to pull the LocalStack, PostgreSQL, or Redis images.
