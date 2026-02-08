using System;

namespace Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Inbound event from Shipping Service when shipment is created.
/// </summary>
public record ShippingCreatedEvent
{
    public Guid ShipmentId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string ServiceLevel { get; init; } = string.Empty;
    public DateTime? EstimatedDelivery { get; init; }
    public DateTime OccurredAt { get; init; }
}
