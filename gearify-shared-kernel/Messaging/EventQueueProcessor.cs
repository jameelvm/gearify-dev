using Gearify.SharedKernel.Correlation;
using Gearify.SharedKernel.Messaging.Idempotency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Gearify.SharedKernel.Messaging;

/// <summary>
/// Generic background service that polls an IEventQueue and delegates
/// message processing to an IEventHandler.
/// </summary>
public class EventQueueProcessor<T> : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventQueueProcessor<T>> _logger;

    public EventQueueProcessor(
        IServiceProvider serviceProvider,
        ILogger<EventQueueProcessor<T>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var typeName = typeof(T).Name;
        _logger.LogInformation("EventQueueProcessor<{EventType}> started", typeName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EventQueueProcessor<{EventType}>", typeName);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("EventQueueProcessor<{EventType}> stopped", typeName);
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IEventQueue<T>>();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<T>>();

        // Get idempotency store - falls back to NoOpIdempotencyStore if not registered
        var idempotencyStore = scope.ServiceProvider.GetService<IIdempotencyStore>()
            ?? new NoOpIdempotencyStore();

        var messages = await queue.ReceiveMessagesAsync(
            maxMessages: 10,
            waitTimeSeconds: 20,
            cancellationToken: cancellationToken);

        if (!messages.Any())
        {
            return;
        }

        _logger.LogInformation("Received {Count} {EventType} messages", messages.Count, typeof(T).Name);

        foreach (var message in messages)
        {
            await ProcessSingleMessageAsync(message, queue, handler, idempotencyStore, cancellationToken);
        }
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<T> queueMessage,
        IEventQueue<T> queue,
        IEventHandler<T> handler,
        IIdempotencyStore idempotencyStore,
        CancellationToken cancellationToken)
    {
        var eventId = queueMessage.EventId;
        var typeName = typeof(T).Name;
        var correlationId = queueMessage.CorrelationId;

        if (!string.IsNullOrEmpty(correlationId))
            CorrelationContext.Set(correlationId);

        using var _ = LogContext.PushProperty("CorrelationId", correlationId);

        try
        {
            // Check for duplicate using atomic claim
            if (!string.IsNullOrEmpty(eventId))
            {
                var claimed = await idempotencyStore.TryClaimEventAsync(eventId, cancellationToken);
                if (!claimed)
                {
                    // Duplicate message - already processed
                    _logger.LogInformation(
                        "Skipping duplicate {EventType} event {EventId} (MessageId: {MessageId})",
                        typeName,
                        eventId,
                        queueMessage.MessageId);

                    await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
                    return;
                }
            }
            else
            {
                _logger.LogWarning(
                    "Message {MessageId} has no EventId - cannot check for duplicates",
                    queueMessage.MessageId);
            }

            // Process the message
            var shouldDelete = await handler.HandleAsync(queueMessage.Body, cancellationToken);

            if (shouldDelete)
            {
                // Mark as processed (in case TryClaimEventAsync only does a temporary claim)
                if (!string.IsNullOrEmpty(eventId))
                {
                    await idempotencyStore.MarkAsProcessedAsync(eventId, cancellationToken);
                }

                await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);

                _logger.LogDebug(
                    "Successfully processed {EventType} event {EventId}",
                    typeName,
                    eventId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing {EventType} event {EventId} (MessageId: {MessageId}). Message will be retried.",
                typeName,
                eventId,
                queueMessage.MessageId);

            // Release the idempotency claim so the message can be retried by SQS.
            // Without this, the claim would persist (up to TTL) and retries would
            // be incorrectly treated as duplicates, causing silent message loss.
            if (!string.IsNullOrEmpty(eventId))
            {
                await idempotencyStore.ReleaseClaimAsync(eventId, cancellationToken);
            }
        }
    }
}
