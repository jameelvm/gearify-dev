using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles RefundCompletedEvent from Payment Service.
/// Updates order status to Refunded and marks saga as completed.
/// </summary>
public class RefundCompletedEventHandler : IEventHandler<RefundCompletedEvent>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ILogger<RefundCompletedEventHandler> _logger;

    public RefundCompletedEventHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ILogger<RefundCompletedEventHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(RefundCompletedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing RefundCompletedEvent for Order {OrderId}, Refund {RefundId}, Amount {Amount} {Currency}",
            evt.OrderId,
            evt.RefundId,
            evt.Amount,
            evt.Currency);

        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);
        var order = await unitOfWork.Orders.GetByIdAsync(evt.OrderId, evt.TenantId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for refund completed event", evt.OrderId);
            return true;
        }

        // Verify order is in a valid state for refund completion
        if (order.Status != OrderStatus.Cancelled)
        {
            _logger.LogWarning(
                "Order {OrderId} received RefundCompletedEvent but status is {Status}, not Cancelled. Skipping.",
                evt.OrderId, order.Status);
            return true;
        }

        var previousStatus = order.Status;

        // Update order to Refunded status
        order.Status = OrderStatus.Refunded;
        order.SagaState = SagaState.Completed;
        order.UpdatedAt = DateTime.UtcNow;

        // Add status history
        await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus.ToString(),
            ToStatus = OrderStatus.Refunded.ToString(),
            Reason = $"Refund completed. Amount: {evt.Amount} {evt.Currency}. RefundId: {evt.RefundId}",
            ChangedBy = "System"
        }, cancellationToken);

        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} marked as Refunded. Amount: {Amount} {Currency}. Saga completed.",
            order.Id, evt.Amount, evt.Currency);

        return true;
    }
}
