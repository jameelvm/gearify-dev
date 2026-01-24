using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            await ProcessSingleMessageAsync(message, queue, handler, cancellationToken);
        }
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<T> queueMessage,
        IEventQueue<T> queue,
        IEventHandler<T> handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var shouldDelete = await handler.HandleAsync(queueMessage.Body, cancellationToken);

            if (shouldDelete)
            {
                await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing {EventType} message {MessageId}. Message will be retried.",
                typeof(T).Name,
                queueMessage.MessageId);
        }
    }
}
