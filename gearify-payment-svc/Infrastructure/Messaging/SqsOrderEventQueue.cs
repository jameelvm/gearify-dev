using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.PaymentService.Infrastructure.Configuration;
using Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.PaymentService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation for receiving order events from Order Service
/// </summary>
public class SqsOrderEventQueue : IEventQueue<OrderCreatedEventMessage>
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsOrderEventQueue> _logger;
    private readonly string _queueUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SqsOrderEventQueue(
        IAmazonSQS sqsClient,
        IOptions<MessagingConfiguration> messagingSettings,
        ILogger<SqsOrderEventQueue> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _queueUrl = messagingSettings.Value.SQS.OrderCreatedQueueUrl;

        if (string.IsNullOrEmpty(_queueUrl))
        {
            _logger.LogWarning("OrderCreatedQueueUrl is not configured");
        }
        else
        {
            _logger.LogInformation("Using configured queue URL: {QueueUrl}", _queueUrl);
        }
    }

    public async Task<List<QueueMessage<OrderCreatedEventMessage>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_queueUrl))
        {
            return new List<QueueMessage<OrderCreatedEventMessage>>();
        }

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
            var messages = new List<QueueMessage<OrderCreatedEventMessage>>();

            foreach (var message in response.Messages)
            {
                try
                {
                    // SNS wraps the message in a JSON envelope
                    var snsMessage = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body, JsonOptions);

                    if (snsMessage?.Message != null)
                    {
                        // Parse event envelope from SNS message
                        var envelope = JsonSerializer.Deserialize<OrderEventEnvelope>(snsMessage.Message, JsonOptions);

                        if (envelope is { Payload: not null, EventType: "OrderCreatedEvent" })
                        {
                            var payloadJson = JsonSerializer.Serialize(envelope.Payload, JsonOptions);
                            var orderEvent = JsonSerializer.Deserialize<OrderCreatedEventMessage>(payloadJson, JsonOptions);

                            if (orderEvent != null)
                            {
                                messages.Add(new QueueMessage<OrderCreatedEventMessage>
                                {
                                    MessageId = message.MessageId,
                                    ReceiptHandle = message.ReceiptHandle,
                                    Body = orderEvent
                                });
                            }
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
        if (string.IsNullOrEmpty(_queueUrl))
        {
            return;
        }

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

    /// <summary>
    /// Order event envelope from Order Service
    /// </summary>
    private class OrderEventEnvelope
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public object? Payload { get; set; }
    }
}
