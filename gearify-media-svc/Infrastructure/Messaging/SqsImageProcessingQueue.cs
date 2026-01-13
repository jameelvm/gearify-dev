using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.MediaService.Infrastructure.Configuration;
using Gearify.MediaService.Infrastructure.Messaging.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation of image processing queue
/// </summary>
public class SqsImageProcessingQueue : IImageProcessingQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsImageProcessingQueue> _logger;
    private readonly string _queueUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SqsImageProcessingQueue(
        IAmazonSQS sqsClient,
        IOptions<MessagingConfiguration> messagingSettings,
        ILogger<SqsImageProcessingQueue> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;

        // Use configured queue URL
        _queueUrl = messagingSettings.Value.SQS.ImageProcessingQueueUrl;

        if (string.IsNullOrEmpty(_queueUrl))
        {
            throw new InvalidOperationException("ImageProcessingQueueUrl is not configured in Messaging:SQS section");
        }

        _logger.LogInformation("Using configured queue URL: {QueueUrl}", _queueUrl);
    }

    public async Task<List<QueueMessage<ImageProcessingMessage>>> ReceiveMessagesAsync(
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
                MessageAttributeNames = new List<string> { "All" }
            };

            var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

            return response.Messages.Select(msg =>
            {
                // SNS wraps the message, so we need to extract it
                var messageBody = ExtractSnsMessage(msg.Body);
                var processingMessage = JsonSerializer.Deserialize<ImageProcessingMessage>(messageBody, JsonOptions);

                return new QueueMessage<ImageProcessingMessage>
                {
                    Body = processingMessage ?? new ImageProcessingMessage(),
                    ReceiptHandle = msg.ReceiptHandle,
                    MessageId = msg.MessageId
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from SQS queue");
            return new List<QueueMessage<ImageProcessingMessage>>();
        }
    }

    public async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sqsClient.DeleteMessageAsync(_queueUrl, receiptHandle, cancellationToken);

            _logger.LogDebug("Deleted message from queue. ReceiptHandle: {ReceiptHandle}", receiptHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting message from SQS queue");
            throw;
        }
    }

    public async Task ReturnMessageAsync(string receiptHandle, int visibilityTimeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new ChangeMessageVisibilityRequest
            {
                QueueUrl = _queueUrl,
                ReceiptHandle = receiptHandle,
                VisibilityTimeout = visibilityTimeoutSeconds
            };

            await _sqsClient.ChangeMessageVisibilityAsync(request, cancellationToken);

            _logger.LogDebug("Returned message to queue with {Seconds}s visibility timeout", visibilityTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error returning message to SQS queue");
            throw;
        }
    }

    private string ExtractSnsMessage(string messageBody)
    {
        try
        {
            // SNS sends messages wrapped in JSON
            using var doc = JsonDocument.Parse(messageBody);
            if (doc.RootElement.TryGetProperty("Message", out var messageElement))
            {
                return messageElement.GetString() ?? messageBody;
            }
            return messageBody;
        }
        catch
        {
            // If not SNS format, return as-is
            return messageBody;
        }
    }
}
