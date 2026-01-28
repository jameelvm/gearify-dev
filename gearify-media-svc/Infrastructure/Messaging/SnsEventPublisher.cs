using Amazon.SimpleNotificationService;
using Gearify.MediaService.Domain.Events;
using Gearify.MediaService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// Media Service SNS event publisher.
/// Publishes media domain events with standardized envelope.
/// Routes different event types to their respective topics.
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
        return eventType switch
        {
            nameof(MediaUploadedEvent) => _settings.SNS.MediaUploadedTopicArn,
            nameof(ImageProcessingCompletedEvent) => _settings.SNS.ImageProcessingCompletedTopicArn,
            _ => null
        };
    }
}
