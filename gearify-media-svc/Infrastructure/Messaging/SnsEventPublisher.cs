using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.MediaService.Application.Events;
using Gearify.MediaService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of event publisher
/// </summary>
public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly MessagingSettings _messagingSettings;
    private readonly ILogger<SnsEventPublisher> _logger;

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingSettings> messagingSettings,
        ILogger<SnsEventPublisher> logger)
    {
        _snsClient = snsClient;
        _messagingSettings = messagingSettings.Value;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var topicArn = GetTopicArnForEventType<T>();
            var message = JsonSerializer.Serialize(eventData);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                Subject = typeof(T).Name
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Published event {EventType} to topic ARN {TopicArn}. MessageId: {MessageId}",
                typeof(T).Name,
                topicArn,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", typeof(T).Name);
            throw;
        }
    }

    private string GetTopicArnForEventType<T>()
    {
        // Map event type to configured topic ARN
        var topicArn = typeof(T).Name switch
        {
            nameof(MediaUploadedEvent) => _messagingSettings.SNS.MediaUploadedTopicArn,
            nameof(ImageProcessingCompletedEvent) => _messagingSettings.SNS.ImageProcessingCompletedTopicArn,
            _ => throw new InvalidOperationException($"No topic ARN configured for event type {typeof(T).Name}")
        };

        if (string.IsNullOrEmpty(topicArn))
        {
            throw new InvalidOperationException($"Topic ARN for event type {typeof(T).Name} is not configured in appsettings");
        }

        return topicArn;
    }
}
