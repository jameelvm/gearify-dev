using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;

namespace Gearify.SharedKernel.Events;

/// <summary>
/// Base class for SNS event publishers that standardizes envelope wrapping.
/// Services inherit and provide topic ARN routing and tenant ID extraction.
/// </summary>
public abstract class SnsEventPublisherBase : ISnsEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    protected SnsEventPublisherBase(
        IAmazonSimpleNotificationService snsClient,
        ILogger logger)
    {
        _snsClient = snsClient;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var topicArn = GetTopicArn(eventType);

        if (string.IsNullOrEmpty(topicArn))
        {
            _logger.LogWarning("Topic ARN not configured for event type {EventType}, skipping publish", eventType);
            return;
        }

        try
        {
            var tenantId = GetTenantId(domainEvent);
            var envelope = EventEnvelope.Wrap(domainEvent, tenantId);
            var message = JsonSerializer.Serialize(envelope, JsonOptions);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                Subject = eventType,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EventType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = eventType
                    }
                }
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Published {EventType} to topic {TopicArn}. MessageId: {MessageId}",
                eventType,
                topicArn,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} to SNS: TopicArn={TopicArn}",
                eventType,
                topicArn);

            // Don't throw - event publishing failure shouldn't fail the main operation
        }
    }

    /// <summary>
    /// Get the SNS topic ARN for the given event type.
    /// Override in derived class to provide service-specific routing.
    /// </summary>
    protected abstract string? GetTopicArn(string eventType);

    /// <summary>
    /// Extract tenant ID from the domain event.
    /// Override in derived class if events contain tenant information.
    /// </summary>
    protected virtual string GetTenantId<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        // Default: try to get TenantId via reflection (common pattern)
        var tenantIdProperty = typeof(TEvent).GetProperty("TenantId");
        if (tenantIdProperty != null)
        {
            return tenantIdProperty.GetValue(domainEvent)?.ToString() ?? string.Empty;
        }
        return string.Empty;
    }
}
