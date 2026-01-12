using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.MediaService.Domain.Events;
using Gearify.MediaService.Infrastructure.Configuration;
using Gearify.SharedKernel.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of ISnsEventPublisher.
/// Handles topic routing and publishing for all domain events.
/// </summary>
public class SnsEventPublisher : ISnsEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly MessagingSettings _messagingSettings;
    private readonly ILogger<SnsEventPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IOptions<MessagingSettings> messagingSettings,
        ILogger<SnsEventPublisher> logger)
    {
        _snsClient = snsClient;
        _messagingSettings = messagingSettings.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        var topicArn = GetTopicArn<TEvent>();

        if (string.IsNullOrEmpty(topicArn))
        {
            _logger.LogWarning(
                "Topic ARN not configured for event type {EventType}, skipping publish",
                typeof(TEvent).Name);
            return;
        }

        try
        {
            var message = JsonSerializer.Serialize(domainEvent, JsonOptions);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                Subject = typeof(TEvent).Name
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
                "Error publishing {EventType} to topic {TopicArn}",
                typeof(TEvent).Name,
                topicArn);
            throw;
        }
    }

    private string GetTopicArn<TEvent>() where TEvent : IDomainEvent
    {
        return typeof(TEvent).Name switch
        {
            nameof(MediaUploadedEvent) => _messagingSettings.SNS.MediaUploadedTopicArn,
            nameof(ImageProcessingCompletedEvent) => _messagingSettings.SNS.ImageProcessingCompletedTopicArn,
            _ => throw new InvalidOperationException($"No topic ARN configured for event type {typeof(TEvent).Name}")
        };
    }
}
