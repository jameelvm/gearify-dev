using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Messaging;
using Gearify.ShippingService.Events;
using Gearify.ShippingService.Infrastructure.Messaging.Events.Inbound;
using Microsoft.Extensions.Logging;

namespace Gearify.ShippingService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles OrderConfirmedEvent from Order Service.
/// Creates shipment and publishes shipping events.
/// For automated flow, simulates immediate shipment and delivery.
/// </summary>
public class OrderConfirmedEventHandler : IEventHandler<OrderConfirmedEvent>
{
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<OrderConfirmedEventHandler> _logger;

    public OrderConfirmedEventHandler(
        ISnsEventPublisher eventPublisher,
        ILogger<OrderConfirmedEventHandler> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(OrderConfirmedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing OrderConfirmedEvent for Order {OrderId} ({OrderNumber})",
            evt.OrderId,
            evt.OrderNumber);

        var shipmentId = Guid.NewGuid();
        var trackingNumber = GenerateTrackingNumber();

        // Step 1: Create shipment (label generated)
        var createdEvent = new ShippingCreatedEvent
        {
            ShipmentId = shipmentId,
            TenantId = evt.TenantId,
            OrderId = evt.OrderId,
            OrderNumber = evt.OrderNumber,
            UserId = evt.UserId,
            Carrier = "USPS",
            ServiceLevel = "Priority",
            EstimatedDelivery = DateTime.UtcNow.AddDays(3),
            ShippingAddress = new ShippingCreatedEvent.ShippingAddressInfo
            {
                FullName = evt.ShippingAddress?.FullName ?? "",
                Street = evt.ShippingAddress?.Street ?? "",
                Street2 = evt.ShippingAddress?.Street2,
                City = evt.ShippingAddress?.City ?? "",
                State = evt.ShippingAddress?.State ?? "",
                PostalCode = evt.ShippingAddress?.PostalCode ?? "",
                Country = evt.ShippingAddress?.Country ?? "",
                Phone = evt.ShippingAddress?.Phone
            }
        };

        await _eventPublisher.PublishAsync(createdEvent, cancellationToken);
        _logger.LogInformation("Published ShippingCreatedEvent for Order {OrderId}", evt.OrderId);

        // Step 2: Simulate carrier pickup (shipped)
        var shippedEvent = new ShippingShippedEvent
        {
            ShipmentId = shipmentId,
            TenantId = evt.TenantId,
            OrderId = evt.OrderId,
            OrderNumber = evt.OrderNumber,
            TrackingNumber = trackingNumber,
            Carrier = "USPS",
            TrackingUrl = $"https://tools.usps.com/go/TrackConfirmAction?tLabels={trackingNumber}",
            EstimatedDelivery = DateTime.UtcNow.AddDays(3)
        };

        await _eventPublisher.PublishAsync(shippedEvent, cancellationToken);
        _logger.LogInformation("Published ShippingShippedEvent for Order {OrderId}, Tracking: {TrackingNumber}",
            evt.OrderId, trackingNumber);

        // Step 3: For automated testing, simulate immediate delivery
        var deliveredEvent = new ShippingDeliveredEvent
        {
            ShipmentId = shipmentId,
            TenantId = evt.TenantId,
            OrderId = evt.OrderId,
            OrderNumber = evt.OrderNumber,
            UserId = evt.UserId,
            DeliveredTo = evt.ShippingAddress?.FullName ?? "Customer",
            DeliveredAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(deliveredEvent, cancellationToken);
        _logger.LogInformation("Published ShippingDeliveredEvent for Order {OrderId}", evt.OrderId);

        return true;
    }

    private static string GenerateTrackingNumber()
    {
        return $"9400{DateTime.UtcNow:yyyyMMdd}{Guid.NewGuid().ToString("N")[..10].ToUpper()}";
    }
}
