using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.SharedKernel.AI.Events;

/// <summary>
/// Background service that long-polls the SQS user events queue
/// and persists events to DynamoDB for analytics and ML training.
/// </summary>
public class UserInteractionProcessor : BackgroundService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly ILogger<UserInteractionProcessor> _logger;
    private readonly AIServiceConfiguration _config;

    private const int MaxBatchSize = 10;
    private const int LongPollWaitSeconds = 20;
    private const int ErrorBackoffSeconds = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public UserInteractionProcessor(
        IAmazonSQS sqsClient,
        IAmazonDynamoDB dynamoClient,
        ILogger<UserInteractionProcessor> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _sqsClient = sqsClient;
        _dynamoClient = dynamoClient;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_config.UserEventsQueueUrl))
        {
            _logger.LogWarning("UserEventsQueueUrl not configured, UserInteractionProcessor is disabled");
            return;
        }

        _logger.LogInformation(
            "UserInteractionProcessor started, polling queue {QueueUrl}",
            _config.UserEventsQueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = _config.UserEventsQueueUrl,
                    MaxNumberOfMessages = MaxBatchSize,
                    WaitTimeSeconds = LongPollWaitSeconds,
                    AttributeNames = new List<string> { "All" }
                }, stoppingToken);

                if (response.Messages.Count == 0)
                    continue;

                _logger.LogDebug("Received {Count} messages from SQS", response.Messages.Count);

                var deleteEntries = new List<DeleteMessageBatchRequestEntry>();

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize<UserInteractionEvent>(
                            message.Body, JsonOptions);

                        if (evt is null) continue;

                        await PersistEventAsync(evt, stoppingToken);

                        deleteEntries.Add(new DeleteMessageBatchRequestEntry
                        {
                            Id = message.MessageId,
                            ReceiptHandle = message.ReceiptHandle
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to process message {MessageId}, will retry",
                            message.MessageId);
                    }
                }

                if (deleteEntries.Count > 0)
                {
                    await _sqsClient.DeleteMessageBatchAsync(new DeleteMessageBatchRequest
                    {
                        QueueUrl = _config.UserEventsQueueUrl,
                        Entries = deleteEntries
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UserInteractionProcessor polling loop");
                await Task.Delay(TimeSpan.FromSeconds(ErrorBackoffSeconds), stoppingToken);
            }
        }

        _logger.LogInformation("UserInteractionProcessor stopped");
    }

    private async Task PersistEventAsync(UserInteractionEvent evt, CancellationToken cancellationToken)
    {
        var tableName = _config.UserEventsTableName ?? "gearify-user-events";
        var timestampMs = new DateTimeOffset(evt.Timestamp).ToUnixTimeMilliseconds();

        var item = new Dictionary<string, AttributeValue>
        {
            ["UserId"] = new() { S = evt.UserId },
            ["Timestamp"] = new() { N = timestampMs.ToString() },
            ["TenantId"] = new() { S = evt.TenantId },
            ["EventType"] = new() { S = evt.EventType },
            ["SessionId"] = new() { S = evt.SessionId }
        };

        if (!string.IsNullOrEmpty(evt.ProductId))
            item["ProductId"] = new AttributeValue { S = evt.ProductId };

        if (evt.EventValue.HasValue)
            item["EventValue"] = new AttributeValue { N = evt.EventValue.Value.ToString() };

        if (evt.Metadata.Count > 0)
        {
            item["Metadata"] = new AttributeValue
            {
                M = evt.Metadata.ToDictionary(
                    kv => kv.Key,
                    kv => new AttributeValue { S = kv.Value })
            };
        }

        // TTL: 90 days from event timestamp
        var ttlEpoch = new DateTimeOffset(evt.Timestamp).AddDays(90).ToUnixTimeSeconds();
        item["TTL"] = new AttributeValue { N = ttlEpoch.ToString() };

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = item
        }, cancellationToken);

        _logger.LogDebug(
            "Persisted {EventType} event for user {UserId} to DynamoDB",
            evt.EventType, evt.UserId);
    }
}
