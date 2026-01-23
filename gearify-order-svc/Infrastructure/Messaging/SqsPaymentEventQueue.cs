using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gearify.OrderService.Infrastructure.Configuration;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.OrderService.Infrastructure.Messaging;

/// <summary>
/// SQS implementation for receiving payment events from Payment Service
/// </summary>
public class SqsPaymentEventQueue : IEventQueue<PaymentEventMessage>
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsPaymentEventQueue> _logger;
    private readonly string _queueUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SqsPaymentEventQueue(
        IAmazonSQS sqsClient,
        IOptions<MessagingConfiguration> messagingSettings,
        ILogger<SqsPaymentEventQueue> logger)
    {
        _sqsClient = sqsClient;
        _logger = logger;
        _queueUrl = messagingSettings.Value.SQS.PaymentProcessedQueueUrl;

        if (string.IsNullOrEmpty(_queueUrl))
        {
            _logger.LogWarning("PaymentProcessedQueueUrl is not configured");
        }
        else
        {
            _logger.LogInformation("Using configured queue URL: {QueueUrl}", _queueUrl);
        }
    }

    public async Task<List<QueueMessage<PaymentEventMessage>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_queueUrl))
        {
            return new List<QueueMessage<PaymentEventMessage>>();
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
            var messages = new List<QueueMessage<PaymentEventMessage>>();

            foreach (var message in response.Messages)
            {
                try
                {
                    // SNS wraps the message in a JSON envelope
                    var snsMessage = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body, JsonOptions);

                    if (snsMessage?.Message != null)
                    {
                        // Parse event envelope from SNS message
                        var envelope = JsonSerializer.Deserialize<PaymentEventEnvelope>(snsMessage.Message, JsonOptions);

                        if (envelope?.Payload != null)
                        {
                            var payloadJson = JsonSerializer.Serialize(envelope.Payload, JsonOptions);
                            var paymentEvent = JsonSerializer.Deserialize<PaymentEventMessage>(payloadJson, JsonOptions);

                            if (paymentEvent != null)
                            {
                                // Set the event type from the envelope
                                var eventWithType = paymentEvent with { EventType = envelope.EventType };

                                messages.Add(new QueueMessage<PaymentEventMessage>
                                {
                                    MessageId = message.MessageId,
                                    ReceiptHandle = message.ReceiptHandle,
                                    Body = eventWithType
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
                ReceiptHandle = receiptHandlei
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
    /// Payment event envelope from Payment Service
    /// </summary>
    private class PaymentEventEnvelope
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public object? Payload { get; set; }
    }
}
