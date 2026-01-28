using Amazon.SimpleNotificationService;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// Catalog Service SNS event publisher.
/// Publishes catalog domain events with standardized envelope.
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
        // All catalog events go to the same topic
        return _settings.SNS.CatalogEventsTopicArn;
    }
}
