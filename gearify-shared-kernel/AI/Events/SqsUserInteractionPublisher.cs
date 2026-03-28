using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.SharedKernel.AI.Events;

public class SqsUserInteractionPublisher : IUserInteractionPublisher
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsUserInteractionPublisher> _logger;
    private readonly AIServiceConfiguration _config;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SqsUserInteractionPublisher(
        IAmazonSQS sqsClient,
        ILogger<SqsUserInteractionPublisher> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _config = config.Value;
    }

    public async Task PublishAsync(UserInteractionEvent evt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.UserEventsQueueUrl))
        {
            _logger.LogDebug("UserEventsQueueUrl not configured, skipping event publish");
            return;
        }

        try
        {
            var messageBody = JsonSerializer.Serialize(evt, JsonOptions);

            var request = new SendMessageRequest
            {
                QueueUrl = _config.UserEventsQueueUrl,
                MessageBody = messageBody,
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["EventType"] = new()
                    {
                        DataType = "String",
                        StringValue = evt.EventType
                    },
                    ["TenantId"] = new()
                    {
                        DataType = "String",
                        StringValue = evt.TenantId
                    }
                }
            };

            await _sqsClient.SendMessageAsync(request, cancellationToken);

            _logger.LogDebug(
                "Published {EventType} event for user {UserId}, product {ProductId}",
                evt.EventType, evt.UserId, evt.ProductId);
        }
        catch (Exception ex)
        {
            // Never throw — event tracking must not block user requests
            _logger.LogWarning(ex,
                "Failed to publish {EventType} event for user {UserId}",
                evt.EventType, evt.UserId);
        }
    }
}
