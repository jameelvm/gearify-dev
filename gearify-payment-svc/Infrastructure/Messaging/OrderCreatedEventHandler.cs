using System.Threading;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Commands;
using Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.Messaging;

/// <summary>
/// Handles order created events received from Order Service.
/// Triggers payment processing when a new order is created.
/// </summary>
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEventMessage>
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IMediator mediator,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(OrderCreatedEventMessage evt, CancellationToken cancellationToken = default)
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

        var result = await _mediator.Send(command, cancellationToken);

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

        return true;
    }
}
