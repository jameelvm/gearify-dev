using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace Gearify.SharedKernel.Messaging;

/// <summary>
/// Generic SQS event queue consumer that handles SNS message envelope deserialization.
/// Supports optional event type filtering to process only specific event types.
/// </summary>
/// <typeparam name="T">The event message type to deserialize the payload into</typeparam>
public class SqsEventQueue<T> : IEventQueue<T> where T : class
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ILogger<SqsEventQueue<T>> _logger;
    private readonly string _queueUrl;
    private readonly HashSet<string>? _eventTypeFilters;
    private readonly Func<T, string, T>? _eventTypeEnricher;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates a new SQS event queue consumer.
    /// Simplified constructor for single-event-type queues (recommended pattern).
    /// </summary>
    /// <param name="sqsClient">AWS SQS client</param>
    /// <param name="queueUrl">The SQS queue URL to consume from</param>
    /// <param name="logger">Logger instance</param>
    public SqsEventQueue(
        IAmazonSQS sqsClient,
        string queueUrl,
        ILogger<SqsEventQueue<T>> logger)
        : this(sqsClient, queueUrl, logger, eventTypeFilters: null, eventTypeEnricher: null)
    {
    }

    /// <summary>
    /// Creates a new SQS event queue consumer with optional filtering.
    /// Use this constructor only when a single queue receives multiple event types.
    /// For the recommended pattern (one queue per event type), use the simpler constructor.
    /// </summary>
    /// <param name="sqsClient">AWS SQS client</param>
    /// <param name="queueUrl">The SQS queue URL to consume from</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="eventTypeFilters">Optional: only process messages with these event types. If null, processes all events.</param>
    /// <param name="eventTypeEnricher">Optional: function to set EventType on the deserialized message (e.g., (msg, type) => msg with { EventType = type })</param>
    public SqsEventQueue(
        IAmazonSQS sqsClient,
        string queueUrl,
        ILogger<SqsEventQueue<T>> logger,
        IEnumerable<string>? eventTypeFilters,
        Func<T, string, T>? eventTypeEnricher)
    {
        _sqsClient = sqsClient;
        _queueUrl = queueUrl;
        _logger = logger;
        _eventTypeFilters = eventTypeFilters?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _eventTypeEnricher = eventTypeEnricher;

        if (string.IsNullOrEmpty(_queueUrl))
        {
            _logger.LogWarning("SqsEventQueue<{EventType}>: Queue URL is not configured", typeof(T).Name);
        }
        else
        {
            _logger.LogInformation("SqsEventQueue<{EventType}> initialized with queue: {QueueUrl}",
                typeof(T).Name, _queueUrl);
        }
    }

    public async Task<List<QueueMessage<T>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_queueUrl))
        {
            return new List<QueueMessage<T>>();
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
            var messages = new List<QueueMessage<T>>();

            foreach (var message in response.Messages)
            {
                try
                {
                    var result = ProcessMessage(message);
                    if (result != null)
                    {
                        messages.Add(result);
                    }
                    else
                    {
                        // Message was filtered out or failed to parse - delete it
                        await DeleteMessageAsync(message.ReceiptHandle, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
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

    private QueueMessage<T>? ProcessMessage(Message message)
    {
        // SNS wraps the message in a JSON envelope
        var snsMessage = JsonSerializer.Deserialize<SnsMessageEnvelope>(message.Body, JsonOptions);
        if (snsMessage?.Message == null)
        {
            _logger.LogWarning("Message {MessageId} has no SNS Message content", message.MessageId);
            return null;
        }

        // Parse event envelope
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(snsMessage.Message, JsonOptions);
        if (envelope?.Payload == null)
        {
            _logger.LogWarning("Message {MessageId} has no event payload", message.MessageId);
            return null;
        }

        // Check event type filter
        if (_eventTypeFilters != null && !_eventTypeFilters.Contains(envelope.EventType))
        {
            _logger.LogDebug("Message {MessageId} filtered out: EventType {EventType} not in filter list",
                message.MessageId, envelope.EventType);
            return null;
        }

        // Deserialize payload to target type
        var payloadJson = JsonSerializer.Serialize(envelope.Payload, JsonOptions);
        var eventMessage = JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);

        if (eventMessage == null)
        {
            _logger.LogWarning("Message {MessageId} payload could not be deserialized to {Type}",
                message.MessageId, typeof(T).Name);
            return null;
        }

        // Enrich with event type if enricher provided
        if (_eventTypeEnricher != null)
        {
            eventMessage = _eventTypeEnricher(eventMessage, envelope.EventType);
        }

        return new QueueMessage<T>
        {
            MessageId = message.MessageId,
            ReceiptHandle = message.ReceiptHandle,
            Body = eventMessage
        };
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
    /// SNS message envelope - SNS wraps messages in this when sending to SQS
    /// </summary>
    private class SnsMessageEnvelope
    {
        public string? Message { get; set; }
        public string? MessageId { get; set; }
        public string? TopicArn { get; set; }
    }

    /// <summary>
    /// Event envelope from the publishing service
    /// </summary>
    private class EventEnvelope
    {
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public object? Payload { get; set; }
    }
}
