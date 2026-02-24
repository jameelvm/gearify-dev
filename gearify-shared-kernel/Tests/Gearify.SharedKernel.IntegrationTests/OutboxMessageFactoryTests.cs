using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Gearify.SharedKernel.Correlation;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Outbox;
using NSubstitute;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

public class OutboxMessageFactoryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ITopicArnResolver _topicArnResolver;
    private readonly OutboxMessageFactory _factory;

    private record TestOrderCreatedEvent : IDomainEvent
    {
        public Guid OrderId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public OutboxMessageFactoryTests()
    {
        _topicArnResolver = Substitute.For<ITopicArnResolver>();
        _factory = new OutboxMessageFactory(_topicArnResolver);
    }

    [Fact]
    public void CreateOutboxMessage_SetsEventType_And_TopicArn()
    {
        // Arrange
        var topicArn = "arn:aws:sns:us-east-1:000000000000:order-events";
        _topicArnResolver.GetTopicArn("TestOrderCreatedEvent").Returns(topicArn);

        var domainEvent = new TestOrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-1",
            Total = 99.99m
        };

        // Act
        var message = _factory.CreateOutboxMessage(domainEvent);

        // Assert
        message.EventType.Should().Be("TestOrderCreatedEvent");
        message.TopicArn.Should().Be(topicArn);
    }

    [Fact]
    public void CreateOutboxMessage_SerializesPayloadAsEventEnvelope()
    {
        // Arrange
        _topicArnResolver.GetTopicArn("TestOrderCreatedEvent")
            .Returns("arn:aws:sns:us-east-1:000000000000:order-events");

        var domainEvent = new TestOrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-1",
            Total = 150.00m
        };

        // Act
        var message = _factory.CreateOutboxMessage(domainEvent);

        // Assert — payload JSON contains all envelope fields
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Payload, JsonOptions);
        envelope.Should().NotBeNull();
        envelope!.EventId.Should().NotBeNullOrEmpty();
        envelope.EventType.Should().Be("TestOrderCreatedEvent");
        envelope.TenantId.Should().Be("tenant-1");
        envelope.CorrelationId.Should().NotBeNullOrEmpty();
        envelope.Payload.Should().NotBeNull();
    }

    [Fact]
    public void CreateOutboxMessage_PreservesCorrelationContext()
    {
        // Arrange
        var expectedCorrelationId = Guid.NewGuid().ToString();
        CorrelationContext.Set(expectedCorrelationId);

        _topicArnResolver.GetTopicArn("TestOrderCreatedEvent")
            .Returns("arn:aws:sns:us-east-1:000000000000:order-events");

        var domainEvent = new TestOrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-1",
            Total = 75.00m
        };

        try
        {
            // Act
            var message = _factory.CreateOutboxMessage(domainEvent);

            // Assert
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Payload, JsonOptions);
            envelope!.CorrelationId.Should().Be(expectedCorrelationId);
        }
        finally
        {
            // Clean up AsyncLocal
            CorrelationContext.Set(null!);
        }
    }

    [Fact]
    public void CreateOutboxMessage_ThrowsWhenNoTopicArn()
    {
        // Arrange
        _topicArnResolver.GetTopicArn("TestOrderCreatedEvent").Returns((string?)null);

        var domainEvent = new TestOrderCreatedEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-1",
            Total = 50.00m
        };

        // Act & Assert
        var act = () => _factory.CreateOutboxMessage(domainEvent);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*TestOrderCreatedEvent*");
    }
}
