using Amazon.SimpleNotificationService;
using Gearify.SharedKernel.Events;
using Gearify.ShippingService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.ShippingService.Infrastructure.Messaging;

/// <summary>
/// SNS event publisher for Shipping Service.
/// Routes shipping events to gearify-shipping-events topic.
/// </summary>
public class SnsEventPublisher : SnsEventPublisherBase
{
    private readonly MessagingConfiguration _config;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingConfiguration> config,
        ILogger<SnsEventPublisher> logger)
        : base(snsClient, logger)
    {
        _config = config.Value;
    }

    protected override string? GetTopicArn(string eventType)
    {
        // All shipping events go to the shipping-events topic
        return eventType switch
        {
            "ShippingCreatedEvent" => _config.SNS.ShippingEventsTopicArn,
            "ShippingShippedEvent" => _config.SNS.ShippingEventsTopicArn,
            "ShippingStatusUpdatedEvent" => _config.SNS.ShippingEventsTopicArn,
            "ShippingDeliveredEvent" => _config.SNS.ShippingEventsTopicArn,
            _ => null
        };
    }
}
