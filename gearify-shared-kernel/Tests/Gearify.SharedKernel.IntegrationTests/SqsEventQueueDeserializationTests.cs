using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using FluentAssertions;
using Gearify.SharedKernel.IntegrationTests.Fixtures;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

[Collection("LocalStack")]
public class SqsEventQueueDeserializationTests
{
    private readonly LocalStackFixture _localStack;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private record TestPayload
    {
        public Guid OrderId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime OccurredAt { get; init; }
    }

    public SqsEventQueueDeserializationTests(LocalStackFixture localStack)
    {
        _localStack = localStack;
    }

    [Fact]
    public async Task ReceiveMessages_UnwrapsSnsEnvelope_DeserializesPayload()
    {
        // Arrange
        var (topicArn, queueUrl) = await _localStack.SetupTopicWithQueue(
            $"test-topic-{Guid.NewGuid():N}",
            $"test-queue-{Guid.NewGuid():N}");

        var payload = new TestPayload
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-deser",
            Total = 123.45m,
            OccurredAt = DateTime.UtcNow
        };

        var envelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "TestPayload",
            tenantId = "tenant-deser",
            correlationId = Guid.NewGuid().ToString(),
            timestamp = DateTime.UtcNow,
            payload
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, CamelCaseOptions);

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = envelopeJson,
            Subject = "TestPayload"
        });

        var queue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient,
            queueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance);

        // Act — short wait to let SNS deliver to SQS
        await Task.Delay(1000);
        var messages = await queue.ReceiveMessagesAsync(10, 5);

        // Assert
        messages.Should().ContainSingle();
        var msg = messages[0];
        msg.Body.OrderId.Should().Be(payload.OrderId);
        msg.Body.TenantId.Should().Be("tenant-deser");
        msg.Body.Total.Should().Be(123.45m);
    }

    [Fact]
    public async Task ReceiveMessages_ExtractsEventId_And_CorrelationId()
    {
        // Arrange
        var (topicArn, queueUrl) = await _localStack.SetupTopicWithQueue(
            $"test-topic-{Guid.NewGuid():N}",
            $"test-queue-{Guid.NewGuid():N}");

        var expectedEventId = Guid.NewGuid().ToString();
        var expectedCorrelationId = Guid.NewGuid().ToString();

        var envelope = new
        {
            eventId = expectedEventId,
            eventType = "TestPayload",
            tenantId = "tenant-1",
            correlationId = expectedCorrelationId,
            timestamp = DateTime.UtcNow,
            payload = new TestPayload
            {
                OrderId = Guid.NewGuid(),
                TenantId = "tenant-1",
                Total = 50.00m,
                OccurredAt = DateTime.UtcNow
            }
        };

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(envelope, CamelCaseOptions),
            Subject = "TestPayload"
        });

        var queue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient,
            queueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance);

        // Act
        await Task.Delay(1000);
        var messages = await queue.ReceiveMessagesAsync(10, 5);

        // Assert
        messages.Should().ContainSingle();
        messages[0].EventId.Should().Be(expectedEventId);
        messages[0].CorrelationId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task ReceiveMessages_WithFilter_SkipsNonMatchingEventTypes()
    {
        // Arrange
        var (topicArn, queueUrl) = await _localStack.SetupTopicWithQueue(
            $"test-topic-{Guid.NewGuid():N}",
            $"test-queue-{Guid.NewGuid():N}");

        // Publish two messages with different event types
        var matchingEnvelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "TargetEvent",
            tenantId = "t1",
            correlationId = "c1",
            timestamp = DateTime.UtcNow,
            payload = new TestPayload { OrderId = Guid.NewGuid(), TenantId = "t1", Total = 10m, OccurredAt = DateTime.UtcNow }
        };

        var nonMatchingEnvelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "OtherEvent",
            tenantId = "t1",
            correlationId = "c2",
            timestamp = DateTime.UtcNow,
            payload = new TestPayload { OrderId = Guid.NewGuid(), TenantId = "t1", Total = 20m, OccurredAt = DateTime.UtcNow }
        };

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(matchingEnvelope, CamelCaseOptions),
            Subject = "TargetEvent"
        });

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(nonMatchingEnvelope, CamelCaseOptions),
            Subject = "OtherEvent"
        });

        // Queue with event type filter — only "TargetEvent"
        var queue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient,
            queueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance,
            eventTypeFilters: new[] { "TargetEvent" },
            eventTypeEnricher: null);

        // Act
        await Task.Delay(1000);
        var messages = await queue.ReceiveMessagesAsync(10, 5);

        // Assert — only the matching event should be returned
        messages.Should().ContainSingle();
        messages[0].Body.Total.Should().Be(10m);
    }

    [Fact]
    public async Task SnsFilterPolicy_RoutesEventTypes_ToCorrectQueues()
    {
        // Arrange — one topic, two queues with different filter policies
        var topicName = $"routing-topic-{Guid.NewGuid():N}";
        var (topicArn, orderQueueUrl) = await _localStack.SetupTopicWithQueue(
            topicName,
            $"order-queue-{Guid.NewGuid():N}",
            eventTypeFilter: "OrderCreatedEvent");

        // Second queue subscribing to same topic with different filter
        var paymentQueueResponse = await _localStack.SqsClient.CreateQueueAsync(
            new Amazon.SQS.Model.CreateQueueRequest { QueueName = $"payment-queue-{Guid.NewGuid():N}" });
        var paymentQueueUrl = paymentQueueResponse.QueueUrl;

        var paymentQueueAttrs = await _localStack.SqsClient.GetQueueAttributesAsync(
            new Amazon.SQS.Model.GetQueueAttributesRequest
            {
                QueueUrl = paymentQueueUrl,
                AttributeNames = ["QueueArn"]
            });

        await _localStack.SnsClient.SubscribeAsync(new SubscribeRequest
        {
            TopicArn = topicArn,
            Protocol = "sqs",
            Endpoint = paymentQueueAttrs.Attributes["QueueArn"],
            Attributes = new Dictionary<string, string>
            {
                ["RawMessageDelivery"] = "false",
                ["FilterPolicy"] = "{\"EventType\":[\"PaymentCompletedEvent\"]}"
            }
        });

        // Publish OrderCreatedEvent with MessageAttributes (mirrors OutboxPublisher)
        var orderEnvelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "OrderCreatedEvent",
            tenantId = "t1",
            correlationId = "corr-order",
            timestamp = DateTime.UtcNow,
            payload = new TestPayload { OrderId = Guid.NewGuid(), TenantId = "t1", Total = 100m, OccurredAt = DateTime.UtcNow }
        };

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(orderEnvelope, CamelCaseOptions),
            Subject = "OrderCreatedEvent",
            MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
            {
                ["EventType"] = new() { DataType = "String", StringValue = "OrderCreatedEvent" }
            }
        });

        // Publish PaymentCompletedEvent with MessageAttributes
        var paymentEnvelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "PaymentCompletedEvent",
            tenantId = "t1",
            correlationId = "corr-payment",
            timestamp = DateTime.UtcNow,
            payload = new TestPayload { OrderId = Guid.NewGuid(), TenantId = "t1", Total = 200m, OccurredAt = DateTime.UtcNow }
        };

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(paymentEnvelope, CamelCaseOptions),
            Subject = "PaymentCompletedEvent",
            MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
            {
                ["EventType"] = new() { DataType = "String", StringValue = "PaymentCompletedEvent" }
            }
        });

        // Act
        await Task.Delay(2000);

        var orderQueue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient, orderQueueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance);

        var paymentQueue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient, paymentQueueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance);

        var orderMessages = await orderQueue.ReceiveMessagesAsync(10, 3);
        var paymentMessages = await paymentQueue.ReceiveMessagesAsync(10, 3);

        // Assert — each queue only receives its own event type
        orderMessages.Should().ContainSingle();
        orderMessages[0].Body.Total.Should().Be(100m);
        orderMessages[0].CorrelationId.Should().Be("corr-order");

        paymentMessages.Should().ContainSingle();
        paymentMessages[0].Body.Total.Should().Be(200m);
        paymentMessages[0].CorrelationId.Should().Be("corr-payment");
    }

    [Fact]
    public async Task DeleteMessage_RemovesFromQueue()
    {
        // Arrange
        var (topicArn, queueUrl) = await _localStack.SetupTopicWithQueue(
            $"test-topic-{Guid.NewGuid():N}",
            $"test-queue-{Guid.NewGuid():N}");

        var envelope = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "TestPayload",
            tenantId = "t1",
            correlationId = "c1",
            timestamp = DateTime.UtcNow,
            payload = new TestPayload { OrderId = Guid.NewGuid(), TenantId = "t1", Total = 5m, OccurredAt = DateTime.UtcNow }
        };

        await _localStack.SnsClient.PublishAsync(new PublishRequest
        {
            TopicArn = topicArn,
            Message = JsonSerializer.Serialize(envelope, CamelCaseOptions),
            Subject = "TestPayload"
        });

        var queue = new SqsEventQueue<TestPayload>(
            _localStack.SqsClient,
            queueUrl,
            NullLogger<SqsEventQueue<TestPayload>>.Instance);

        await Task.Delay(1000);
        var messages = await queue.ReceiveMessagesAsync(10, 5);
        messages.Should().ContainSingle();

        // Act — delete the message
        await queue.DeleteMessageAsync(messages[0].ReceiptHandle);

        // Assert — queue should be empty now
        var remaining = await queue.ReceiveMessagesAsync(10, 1);
        remaining.Should().BeEmpty();
    }
}
