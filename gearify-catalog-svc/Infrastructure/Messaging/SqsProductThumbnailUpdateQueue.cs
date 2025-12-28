using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.CatalogService.Infrastructure.Messaging.Models;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation for receiving product thumbnail update messages
/// </summary>
public class SqsProductThumbnailUpdateQueue : IProductThumbnailUpdateQueue
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqsProductThumbnailUpdateQueue> _logger;
    private readonly string _queueUrl;

    public SqsProductThumbnailUpdateQueue(
        IAmazonSQS sqsClient,
        IConfiguration configuration,
        ILogger<SqsProductThumbnailUpdateQueue> logger)
    {
        _sqsClient = sqsClient;
        _configuration = configuration;
        _logger = logger;

        var queueName = configuration["AWS:SQS:ProductThumbnailUpdateQueueName"] ?? "gearify-product-thumbnail-update-queue";
        var endpoint = configuration["AWS:SQS:ServiceUrl"] ?? "http://localhost:4566";

        // Construct queue URL for LocalStack
        _queueUrl = $"{endpoint}/000000000000/{queueName}";
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
                    var snsMessage = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body);

                    if (snsMessage?.Message != null)
                    {
                        var eventData = JsonSerializer.Deserialize<ImageProcessingCompletedEvent>(snsMessage.Message);

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
