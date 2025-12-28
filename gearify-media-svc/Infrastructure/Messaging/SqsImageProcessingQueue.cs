using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.MediaService.Application.BackgroundJobs;
using Gearify.MediaService.Application.BackgroundJobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation of image processing queue
/// </summary>
public class SqsImageProcessingQueue : IImageProcessingQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqsImageProcessingQueue> _logger;
    private string? _queueUrl;

    public SqsImageProcessingQueue(
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<SqsImageProcessingQueue> logger)
    {
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<QueueMessage<ImageProcessingMessage>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queueUrl = await GetQueueUrlAsync(cancellationToken);

            var request = new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = maxMessages,
                WaitTimeSeconds = waitTimeSeconds,
                MessageAttributeNames = new List<string> { "All" }
            };

            var response = await _sqsClient.ReceiveMessageAsync(request, cancellationToken);

            return response.Messages.Select(msg =>
            {
                // SNS wraps the message, so we need to extract it
                var messageBody = ExtractSnsMessage(msg.Body);
                var processingMessage = JsonSerializer.Deserialize<ImageProcessingMessage>(messageBody);

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
            var queueUrl = await GetQueueUrlAsync(cancellationToken);

            await _sqsClient.DeleteMessageAsync(queueUrl, receiptHandle, cancellationToken);

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
            var queueUrl = await GetQueueUrlAsync(cancellationToken);

            var request = new ChangeMessageVisibilityRequest
            {
                QueueUrl = queueUrl,
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

    private async Task<string> GetQueueUrlAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_queueUrl))
        {
            return _queueUrl;
        }

        var queueName = _configuration["AWS:SQS:ImageProcessingQueue"] ?? "gearify-image-processing-queue";

        try
        {
            // Try to get existing queue
            var response = await _sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            _queueUrl = response.QueueUrl;
            return _queueUrl;
        }
        catch (QueueDoesNotExistException)
        {
            // Create queue if it doesn't exist
            _logger.LogInformation("Creating SQS queue: {QueueName}", queueName);

            var createRequest = new CreateQueueRequest
            {
                QueueName = queueName,
                Attributes = new Dictionary<string, string>
                {
                    { "VisibilityTimeout", "300" }, // 5 minutes
                    { "MessageRetentionPeriod", "1209600" }, // 14 days
                    { "ReceiveMessageWaitTimeSeconds", "20" } // Long polling
                }
            };

            var createResponse = await _sqsClient.CreateQueueAsync(createRequest, cancellationToken);
            _queueUrl = createResponse.QueueUrl;
            return _queueUrl;
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
