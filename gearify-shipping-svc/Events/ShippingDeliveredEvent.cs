using System;

namespace Gearify.ShippingService.Events;

/// <summary>
/// Published when shipment is delivered.
/// Triggers order completion.
/// </summary>
public record ShippingDeliveredEvent
{
    public Guid ShipmentId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string DeliveredTo { get; init; } = string.Empty;
    public string? SignedBy { get; init; }
    public DateTime DeliveredAt { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
