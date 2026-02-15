using System.Text.Json;
using System.Text.Json.Serialization;
using Gearify.SharedKernel.Events;

namespace Gearify.SharedKernel.Outbox;

public class OutboxMessageFactory : IOutboxMessageFactory
{
    private readonly ITopicArnResolver _topicArnResolver;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public OutboxMessageFactory(ITopicArnResolver topicArnResolver)
    {
        _topicArnResolver = topicArnResolver;
    }

    public OutboxMessage CreateOutboxMessage<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var topicArn = _topicArnResolver.GetTopicArn(eventType);

        if (string.IsNullOrEmpty(topicArn))
            throw new InvalidOperationException($"Topic ARN not configured for event type {eventType}");

        var tenantId = ExtractTenantId(domainEvent);
        var envelope = EventEnvelope.Wrap(domainEvent, tenantId);
        var payload = JsonSerializer.Serialize(envelope, JsonOptions);

        var messageAttributes = JsonSerializer.Serialize(
            new Dictionary<string, string> { ["EventType"] = eventType },
            JsonOptions);

        return new OutboxMessage
        {
            EventType = eventType,
            TopicArn = topicArn,
            Payload = payload,
            MessageAttributes = messageAttributes,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ExtractTenantId<TEvent>(TEvent domainEvent)
    {
        var tenantIdProperty = typeof(TEvent).GetProperty("TenantId");
        return tenantIdProperty?.GetValue(domainEvent)?.ToString() ?? string.Empty;
    }
}
