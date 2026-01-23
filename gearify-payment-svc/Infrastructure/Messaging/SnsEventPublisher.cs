using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.PaymentService.Events;
using Gearify.PaymentService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.PaymentService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of ISnsEventPublisher for payment events.
/// </summary>
public class SnsEventPublisher : ISnsEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly MessagingConfiguration _settings;
    private readonly ILogger<SnsEventPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingConfiguration> settings,
        ILogger<SnsEventPublisher> logger)
    {
        _snsClient = snsClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var topicArn = _settings.SNS.PaymentEventsTopicArn;
        if (string.IsNullOrEmpty(topicArn))
        {
            _logger.LogWarning("PaymentEventsTopicArn is not configured, skipping event publish");
            return;
        }

        try
        {
            var envelope = CreateEventEnvelope(domainEvent);
            var message = JsonSerializer.Serialize(envelope, JsonOptions);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                Subject = typeof(TEvent).Name,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EventType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = typeof(TEvent).Name
                    }
                }
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Published {EventType} to topic {TopicArn}. MessageId: {MessageId}",
                typeof(TEvent).Name,
                topicArn,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish {EventType} to SNS: TopicArn={TopicArn}",
                typeof(TEvent).Name,
                topicArn);

            // Don't throw - event publishing failure shouldn't fail the main operation
        }
    }

    private PaymentEventEnvelope CreateEventEnvelope<TEvent>(TEvent domainEvent)
        where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var (tenantId, occurredAt) = domainEvent switch
        {
            PaymentCompletedEvent e => (e.TenantId, e.OccurredAt),
            PaymentFailedEvent e => (e.TenantId, e.OccurredAt),
            PaymentProcessingEvent e => (e.TenantId, e.OccurredAt),
            RefundInitiatedEvent e => (e.TenantId, e.OccurredAt),
            RefundCompletedEvent e => (e.TenantId, e.OccurredAt),
            _ => (string.Empty, DateTime.UtcNow)
        };

        return new PaymentEventEnvelope
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            TenantId = tenantId,
            Timestamp = occurredAt,
            Payload = domainEvent
        };
    }
}

/// <summary>
/// Payment event envelope for SNS messages
/// </summary>
public class PaymentEventEnvelope
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Payload { get; set; }
}
