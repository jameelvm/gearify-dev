using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Gearify.SharedKernel.Events;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

public class EventEnvelopeSerializationTests
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private record TestEvent : IDomainEvent
    {
        public Guid OrderId { get; init; }
        public string TenantId { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    [Fact]
    public void Wrap_SetsEventType_ToTypeName()
    {
        var domainEvent = new TestEvent { OrderId = Guid.NewGuid(), TenantId = "tenant-1" };

        var envelope = EventEnvelope.Wrap(domainEvent, "tenant-1");

        envelope.EventType.Should().Be("TestEvent");
    }

    [Fact]
    public void Wrap_SetsCorrelationId_WhenProvided()
    {
        var correlationId = Guid.NewGuid().ToString();
        var domainEvent = new TestEvent { OrderId = Guid.NewGuid(), TenantId = "tenant-1" };

        var envelope = EventEnvelope.Wrap(domainEvent, "tenant-1", correlationId);

        envelope.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Wrap_GeneratesUniqueEventId()
    {
        var domainEvent = new TestEvent { OrderId = Guid.NewGuid(), TenantId = "tenant-1" };

        var envelope1 = EventEnvelope.Wrap(domainEvent, "tenant-1");
        var envelope2 = EventEnvelope.Wrap(domainEvent, "tenant-1");

        envelope1.EventId.Should().NotBeNullOrEmpty();
        envelope2.EventId.Should().NotBeNullOrEmpty();
        envelope1.EventId.Should().NotBe(envelope2.EventId);
    }

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        // Arrange — mirrors OutboxMessageFactory serialization
        var correlationId = Guid.NewGuid().ToString();
        var domainEvent = new TestEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-42",
            Total = 199.99m,
            OccurredAt = DateTime.UtcNow
        };

        var envelope = EventEnvelope.Wrap(domainEvent, "tenant-42", correlationId);

        // Act — serialize with camelCase (publisher), deserialize case-insensitive (consumer)
        var json = JsonSerializer.Serialize(envelope, CamelCaseOptions);
        var deserialized = JsonSerializer.Deserialize<EventEnvelope>(json, CaseInsensitiveOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(envelope.EventId);
        deserialized.EventType.Should().Be("TestEvent");
        deserialized.TenantId.Should().Be("tenant-42");
        deserialized.CorrelationId.Should().Be(correlationId);
        deserialized.Timestamp.Should().BeCloseTo(envelope.Timestamp, TimeSpan.FromMilliseconds(1));
        deserialized.Payload.Should().NotBeNull();
    }

    [Fact]
    public void RoundTrip_NestedPayload_CanBeReDeserialized()
    {
        // Arrange
        var domainEvent = new TestEvent
        {
            OrderId = Guid.NewGuid(),
            TenantId = "tenant-x",
            Total = 42.50m,
            OccurredAt = DateTime.UtcNow
        };

        var envelope = EventEnvelope.Wrap(domainEvent, "tenant-x", "corr-123");

        // Act — double-deserialization: JSON → EventEnvelope → payload JSON → TestEvent
        // This mirrors the SqsEventQueue flow
        var envelopeJson = JsonSerializer.Serialize(envelope, CamelCaseOptions);
        var deserializedEnvelope = JsonSerializer.Deserialize<EventEnvelope>(envelopeJson, CaseInsensitiveOptions)!;

        var payloadJson = JsonSerializer.Serialize(deserializedEnvelope.Payload, CaseInsensitiveOptions);
        var deserializedEvent = JsonSerializer.Deserialize<TestEvent>(payloadJson, CaseInsensitiveOptions);

        // Assert
        deserializedEvent.Should().NotBeNull();
        deserializedEvent!.OrderId.Should().Be(domainEvent.OrderId);
        deserializedEvent.TenantId.Should().Be("tenant-x");
        deserializedEvent.Total.Should().Be(42.50m);
    }
}
