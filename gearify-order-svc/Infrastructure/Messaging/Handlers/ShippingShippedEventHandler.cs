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
/// Handles ShippingShippedEvent from Shipping Service.
/// Updates order status to Shipped.
/// </summary>
public class ShippingShippedEventHandler : IEventHandler<ShippingShippedEvent>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ILogger<ShippingShippedEventHandler> _logger;

    public ShippingShippedEventHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ILogger<ShippingShippedEventHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ShippingShippedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing ShippingShippedEvent for Order {OrderId}, Tracking: {TrackingNumber}",
            evt.OrderId,
            evt.TrackingNumber);

        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        var order = await unitOfWork.Orders.GetByIdAsync(evt.OrderId, evt.TenantId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for shipping shipped event", evt.OrderId);
            return true;
        }

        if (order.Status == OrderStatus.Shipped || order.Status == OrderStatus.Delivered)
        {
            _logger.LogInformation("Order {OrderId} already in {Status} status, skipping", evt.OrderId, order.Status);
            return true;
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Shipped;
        order.ShipmentId = evt.ShipmentId;
        order.ShippingStatus = "Shipped";
        order.SagaState = SagaState.ShippingCreated;
        order.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = previousStatus.ToString(),
            ToStatus = OrderStatus.Shipped.ToString(),
            Reason = $"Shipment picked up by {evt.Carrier}. Tracking: {evt.TrackingNumber}",
            ChangedBy = "System"
        }, cancellationToken);

        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Order {OrderId} status updated to Shipped. Tracking: {TrackingNumber}, Carrier: {Carrier}",
            evt.OrderId, evt.TrackingNumber, evt.Carrier);

        return true;
    }
}
