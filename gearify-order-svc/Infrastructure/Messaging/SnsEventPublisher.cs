using Amazon.SimpleNotificationService;
using Gearify.OrderService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.OrderService.Infrastructure.Messaging;

/// <summary>
/// Order Service SNS event publisher.
/// Publishes order domain events with standardized envelope.
/// </summary>
public class SnsEventPublisher : SnsEventPublisherBase
{
    private readonly MessagingConfiguration _settings;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingConfiguration> settings,
        ILogger<SnsEventPublisher> logger)
        : base(snsClient, logger)
    {
        _settings = settings.Value;
    }

    protected override string? GetTopicArn(string eventType)
    {
        // All order events go to the same topic
        return _settings.SNS.OrderEventsTopicArn;
    }
}
