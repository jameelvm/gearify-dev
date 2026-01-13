using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.CatalogService.Infrastructure.Messaging.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation for receiving product thumbnail update messages
/// </summary>
public class SqsProductThumbnailUpdateQueue : IProductThumbnailUpdateQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsProductThumbnailUpdateQueue> _logger;
    private readonly string _queueUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SqsProductThumbnailUpdateQueue(
        IAmazonSQS sqsClient,
        IOptions<MessagingSettings> messagingSettings,
        ILogger<SqsProductThumbnailUpdateQueue> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;

        // Use configured queue URL
        _queueUrl = messagingSettings.Value.SQS.ProductThumbnailUpdateQueueUrl;

        if (string.IsNullOrEmpty(_queueUrl))
        {
            throw new InvalidOperationException("ProductThumbnailUpdateQueueUrl is not configured in Messaging:SQS section");
        }

        _logger.LogInformation("Using configured queue URL: {QueueUrl}", _queueUrl);
    }

    public async Task<List<QueueMessage<ImageProcessingCompletedEvent>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = maxMessages,
                WaitTimeSeconds = waitTimeSeconds,
                MessageAttributeNames = ["All"],
                AttributeNames = ["All"]
            };

            var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

            var messages = new List<QueueMessage<ImageProcessingCompletedEvent>>();

            foreach (var message in response.Messages)
            {
                try
                {
                    // SNS wraps the message in a JSON envelope
                    var snsMessage = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body, JsonOptions);

                    if (snsMessage?.Message != null)
                    {
                        var eventData = JsonSerializer.Deserialize<ImageProcessingCompletedEvent>(snsMessage.Message, JsonOptions);

                        if (eventData != null)
                        {
                            messages.Add(new QueueMessage<ImageProcessingCompletedEvent>
                            {
                                MessageId = message.MessageId,
                                ReceiptHandle = message.ReceiptHandle,
                                Body = eventData
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing message {MessageId}", message.MessageId);
                }
            }

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from queue {QueueUrl}", _queueUrl);
            throw;
        }
    }

    public async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DeleteMessageRequest
            {
                QueueUrl = _queueUrl,
                ReceiptHandle = receiptHandle
            };

            await _sqsClient.DeleteMessageAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message with receipt handle {ReceiptHandle}", receiptHandle);
            throw;
        }
    }

    /// <summary>
    /// SNS message wrapper - SNS wraps messages in this envelope when sending to SQS
    /// </summary>
    private class SnsMessageWrapper
    {
        public string? Message { get; set; }
        public string? MessageId { get; set; }
        public string? TopicArn { get; set; }
    }
}
