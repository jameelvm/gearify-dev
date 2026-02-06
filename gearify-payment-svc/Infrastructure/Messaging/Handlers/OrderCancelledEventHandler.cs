using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Commands;
using Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles OrderCancelledEvent from Order Service.
/// If the order was paid (PaymentId is present), triggers refund processing.
/// If no payment was made, no action is needed.
/// </summary>
public class OrderCancelledEventHandler : IEventHandler<OrderCancelledEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderCancelledEventHandler> _logger;

    public OrderCancelledEventHandler(
        IMediator mediator,
        ILogger<OrderCancelledEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(OrderCancelledEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing OrderCancelledEvent for Order {OrderId} ({OrderNumber})",
            evt.OrderId,
            evt.OrderNumber);

        // No payment was made - nothing to refund
        if (evt.PaymentId == null || evt.PaidAmount == null || evt.PaidAmount <= 0)
        {
            _logger.LogInformation(
                "Order {OrderId} cancelled without payment. No refund needed.",
                evt.OrderId);
            return true;
        }

        _logger.LogInformation(
            "Processing refund for cancelled Order {OrderId}. Amount: {Amount} {Currency}",
            evt.OrderId,
            evt.PaidAmount,
            evt.Currency);

        var command = new RefundPaymentCommand(
            TransactionId: evt.PaymentId.Value,
            TenantId: evt.TenantId,
            Amount: evt.PaidAmount.Value,
            Reason: $"Order cancelled: {evt.Reason}",
            OrderId: evt.OrderId,
            OrderNumber: evt.OrderNumber);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Refund processed for Order {OrderId}. RefundId: {RefundId}",
                evt.OrderId,
                result.Refund?.Id);
            return true;
        }

        // Refund failed - keep message in queue for retry
        _logger.LogWarning(
            "Refund failed for Order {OrderId}: {Error}. Will retry.",
            evt.OrderId,
            result.ErrorMessage);

        return false;
    }
}
