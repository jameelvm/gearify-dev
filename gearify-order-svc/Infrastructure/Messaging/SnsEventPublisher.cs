using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.OrderService.Events;
using Gearify.OrderService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.OrderService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of ISnsEventPublisher for order events.
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
        var topicArn = _settings.SNS.OrderEventsTopicArn;
        if (string.IsNullOrEmpty(topicArn))
        {
            _logger.LogWarning("OrderEventsTopicArn is not configured, skipping event publish");
            return;
        }

        try
        {
            var orderEvent = CreateOrderEvent(domainEvent);
            var message = JsonSerializer.Serialize(orderEvent, JsonOptions);

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

    private OrderEventEnvelope CreateOrderEvent<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
    {
        var eventType = typeof(TEvent).Name;
        var tenantId = domainEvent switch
        {
            OrderCreatedEvent e => e.TenantId,
            OrderConfirmedEvent e => e.TenantId,
            OrderCancelledEvent e => e.TenantId,
            OrderStatusChangedEvent e => e.TenantId,
            _ => string.Empty
        };

        return new OrderEventEnvelope
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = eventType,
            TenantId = tenantId,
            Timestamp = domainEvent.OccurredAt,
            Payload = domainEvent
        };
    }
}

/// <summary>
/// Order event envelope for SNS messages
/// </summary>
public class OrderEventEnvelope
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Payload { get; set; }
}
