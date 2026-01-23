using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Commands;
using Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.Messaging;

/// <summary>
/// Background service that listens for order events from Order Service
/// and processes payments when orders are created
/// </summary>
public class OrderEventQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderEventQueueProcessor> _logger;

    public OrderEventQueueProcessor(
        IServiceProvider serviceProvider,
        ILogger<OrderEventQueueProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Event Queue Processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Order Event Queue Processor");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Order Event Queue Processor stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IEventQueue<OrderCreatedEventMessage>>();
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

        _logger.LogInformation("Received {Count} order events", messages.Count);

        // Process messages sequentially
        foreach (var message in messages)
        {
            await ProcessSingleMessageAsync(message, queue, mediator, cancellationToken);
        }
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<OrderCreatedEventMessage> queueMessage,
        IEventQueue<OrderCreatedEventMessage> queue,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var evt = queueMessage.Body;

        try
        {
            _logger.LogInformation(
                "Processing OrderCreatedEvent for Order {OrderId} ({OrderNumber}), total: {Total}",
                evt.OrderId,
                evt.OrderNumber,
                evt.Total);

            var command = new ProcessOrderPaymentCommand(
                evt.OrderId,
                evt.OrderNumber,
                evt.TenantId,
                evt.UserId,
                evt.Total,
                evt.Currency);

            var result = await mediator.Send(command, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Payment completed for Order {OrderId}: TransactionId={TransactionId}",
                    evt.OrderId, result.TransactionId);
            }
            else
            {
                _logger.LogWarning("Payment failed for Order {OrderId}: {Error}",
                    evt.OrderId, result.ErrorMessage);
            }

            // Delete message after processing (success or failure)
            await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing OrderCreatedEvent for Order {OrderId}. Message will be retried.",
                evt.OrderId);

            // Don't delete the message - it will be retried
            // SQS will eventually move it to DLQ if it keeps failing
        }
    }
}
