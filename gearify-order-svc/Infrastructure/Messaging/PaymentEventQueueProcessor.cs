using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Commands;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.Messaging;

/// <summary>
/// Background service that listens for payment events from Payment Service
/// and updates order status when payment completes or fails
/// </summary>
public class PaymentEventQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentEventQueueProcessor> _logger;

    public PaymentEventQueueProcessor(
        IServiceProvider serviceProvider,
        ILogger<PaymentEventQueueProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Event Queue Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Payment Event Queue Processor");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Payment Event Queue Processor stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IEventQueue<PaymentEventMessage>>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Receive messages from queue (long polling)
        var messages = await queue.ReceiveMessagesAsync(
            maxMessages: 10,
            waitTimeSeconds: 20,
            cancellationToken: cancellationToken);

        if (!messages.Any())
        {
            return; // No messages, loop will retry
        }

        _logger.LogInformation("Received {Count} payment events", messages.Count);

        // Process messages sequentially
        foreach (var message in messages)
        {
            await ProcessSingleMessageAsync(message, queue, mediator, cancellationToken);
        }
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<PaymentEventMessage> queueMessage,
        IEventQueue<PaymentEventMessage> queue,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var evt = queueMessage.Body;

        try
        {
            _logger.LogInformation(
                "Processing {EventType} for Order {OrderId}",
                evt.EventType,
                evt.OrderId);

            bool success = evt.EventType switch
            {
                "PaymentCompletedEvent" => await HandlePaymentCompletedAsync(evt, mediator, cancellationToken),
                "PaymentFailedEvent" => await HandlePaymentFailedAsync(evt, mediator, cancellationToken),
                _ => HandleUnknownEvent(evt.EventType)
            };

            if (success)
            {
                await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing {EventType} for Order {OrderId}. Message will be retried.",
                evt.EventType,
                evt.OrderId);

            // Don't delete the message - it will be retried
            // SQS will eventually move it to DLQ if it keeps failing
        }
    }

    private async Task<bool> HandlePaymentCompletedAsync(
        PaymentEventMessage evt,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmOrderCommand(
            evt.OrderId,
            evt.TenantId,
            evt.TransactionId);

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Order {OrderId} confirmed after payment {TransactionId}",
                evt.OrderId, evt.TransactionId);
        }
        else
        {
            _logger.LogWarning("Failed to confirm order {OrderId}: {Error}",
                evt.OrderId, result.ErrorMessage);
        }

        return true; // Delete message even on failure to avoid infinite retries
    }

    private async Task<bool> HandlePaymentFailedAsync(
        PaymentEventMessage evt,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(
            evt.OrderId,
            evt.TenantId,
            $"Payment failed: {evt.ErrorMessage}",
            "System");

        var result = await mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Order {OrderId} cancelled due to payment failure: {Error}",
                evt.OrderId, evt.ErrorMessage);
        }
        else
        {
            _logger.LogWarning("Failed to cancel order {OrderId}: {Error}",
                evt.OrderId, result.ErrorMessage);
        }

        return true; // Delete message even on failure to avoid infinite retries
    }

    private bool HandleUnknownEvent(string eventType)
    {
        _logger.LogWarning("Unknown event type: {EventType}", eventType);
        return true; // Delete unknown messages
    }
}
