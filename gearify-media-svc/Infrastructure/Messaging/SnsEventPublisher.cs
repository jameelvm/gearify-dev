using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Gearify.MediaService.Application.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// SNS implementation of event publisher
/// </summary>
public class SnsEventPublisher : IEventPublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SnsEventPublisher> _logger;
    private readonly Dictionary<string, string> _topicArns = new();

    public SnsEventPublisher(
        IAmazonSimpleNotificationService snsClient,
        IConfiguration configuration,
        ILogger<SnsEventPublisher> _logger)
    {
        _snsClient = snsClient;
        _configuration = configuration;
        this._logger = _logger;
    }

    public async Task PublishAsync<T>(T eventData, string topicName, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var topicArn = await GetTopicArnAsync(topicName, cancellationToken);
            var message = JsonSerializer.Serialize(eventData);

            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                Subject = typeof(T).Name
            };

            var response = await _snsClient.PublishAsync(request, cancellationToken);

            _logger.LogInformation(
                "Published event {EventType} to topic {TopicName}. MessageId: {MessageId}",
                typeof(T).Name,
                topicName,
                response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType} to topic {TopicName}", typeof(T).Name, topicName);
            throw;
        }
    }

    private async Task<string> GetTopicArnAsync(string topicName, CancellationToken cancellationToken)
    {
        // Cache topic ARNs
        if (_topicArns.TryGetValue(topicName, out var cachedArn))
        {
            return cachedArn;
        }

        // Find or create topic
        try
        {
            var response = await _snsClient.CreateTopicAsync(topicName, cancellationToken);
            _topicArns[topicName] = response.TopicArn;
            return response.TopicArn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting/creating SNS topic {TopicName}", topicName);
            throw;
        }
    }
}
