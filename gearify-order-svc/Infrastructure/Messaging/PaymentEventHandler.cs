using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Commands;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.Messaging;

/// <summary>
/// Handles payment events received from Payment Service.
/// Updates order status when payment completes or fails.
/// </summary>
public class PaymentEventHandler : IEventHandler<PaymentEventMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentEventHandler> _logger;

    public PaymentEventHandler(
        IMediator mediator,
        ILogger<PaymentEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(PaymentEventMessage evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing {EventType} for Order {OrderId}",
            evt.EventType,
            evt.OrderId);

        return evt.EventType switch
        {
            "PaymentCompletedEvent" => await HandlePaymentCompletedAsync(evt, cancellationToken),
            "PaymentFailedEvent" => await HandlePaymentFailedAsync(evt, cancellationToken),
            _ => HandleUnknownEvent(evt.EventType)
        };
    }

    private async Task<bool> HandlePaymentCompletedAsync(
        PaymentEventMessage evt,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmOrderCommand(
            evt.OrderId,
            evt.TenantId,
            evt.TransactionId);

        var result = await _mediator.Send(command, cancellationToken);

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

        return true;
    }

    private async Task<bool> HandlePaymentFailedAsync(
        PaymentEventMessage evt,
        CancellationToken cancellationToken)
    {
        var command = new CancelOrderCommand(
            evt.OrderId,
            evt.TenantId,
            $"Payment failed: {evt.ErrorMessage}",
            "System");

        var result = await _mediator.Send(command, cancellationToken);

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

        return true;
    }

    private bool HandleUnknownEvent(string eventType)
    {
        _logger.LogWarning("Unknown event type: {EventType}", eventType);
        return true;
    }
}
