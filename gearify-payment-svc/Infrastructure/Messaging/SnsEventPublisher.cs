using Amazon.SimpleNotificationService;
using Gearify.PaymentService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.PaymentService.Infrastructure.Messaging;

/// <summary>
/// Payment Service SNS event publisher.
/// Publishes payment domain events with standardized envelope.
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
        // All payment events go to the same topic
        return _settings.SNS.PaymentEventsTopicArn;
    }
}
