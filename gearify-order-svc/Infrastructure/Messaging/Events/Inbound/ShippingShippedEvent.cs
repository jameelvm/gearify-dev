using System;

namespace Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Inbound event from Shipping Service when shipment is picked up by carrier.
/// </summary>
public record ShippingShippedEvent
{
    public Guid ShipmentId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string TrackingUrl { get; init; } = string.Empty;
    public DateTime? EstimatedDelivery { get; init; }
    public DateTime OccurredAt { get; init; }
}
