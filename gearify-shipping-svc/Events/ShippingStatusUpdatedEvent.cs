using System;

namespace Gearify.ShippingService.Events;

/// <summary>
/// Published when shipment status is updated.
/// </summary>
public record ShippingStatusUpdatedEvent
{
    public Guid ShipmentId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string PreviousStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
    public string? Location { get; init; }
    public string? Description { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
