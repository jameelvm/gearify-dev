using System;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when order status changes.
/// </summary>
public record OrderStatusChangedEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string PreviousStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
