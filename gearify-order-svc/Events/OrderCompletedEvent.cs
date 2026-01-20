using System;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when an order is completed (delivered).
/// </summary>
public record OrderCompletedEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
