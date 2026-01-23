using System;
using Gearify.SharedKernel.Events;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when an order is cancelled.
/// </summary>
public record OrderCancelledEvent : IDomainEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? CancelledBy { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
