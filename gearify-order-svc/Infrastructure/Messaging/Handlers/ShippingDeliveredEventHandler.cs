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
/// Handles ShippingDeliveredEvent from Shipping Service.
/// Updates order status to Delivered and completes the order.
/// </summary>
public class ShippingDeliveredEventHandler : IEventHandler<ShippingDeliveredEvent>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ILogger<ShippingDeliveredEventHandler> _logger;

    public ShippingDeliveredEventHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ILogger<ShippingDeliveredEventHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ShippingDeliveredEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing ShippingDeliveredEvent for Order {OrderId}",
            evt.OrderId);

        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        var order = await unitOfWork.Orders.GetByIdAsync(evt.OrderId, evt.TenantId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for shipping delivered event", evt.OrderId);
            return true;
        }

        if (order.Status == OrderStatus.Delivered)
        {
            _logger.LogInformation("Order {OrderId} already delivered, skipping", evt.OrderId);
            return true;
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Delivered;
        order.ShippingStatus = "Delivered";
        order.SagaState = SagaState.Completed;
        order.CompletedAt = evt.DeliveredAt;
        order.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus.ToString(),
            ToStatus = OrderStatus.Delivered.ToString(),
            Reason = $"Delivered to {evt.DeliveredTo}" + (evt.SignedBy != null ? $", signed by {evt.SignedBy}" : ""),
            ChangedBy = "System"
        }, cancellationToken);

        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} completed. Delivered to {DeliveredTo} at {DeliveredAt}",
            evt.OrderId, evt.DeliveredTo, evt.DeliveredAt);

        return true;
    }
}
