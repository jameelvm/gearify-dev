using System;
using Gearify.SharedKernel.Events;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when an order is cancelled.
/// If PaymentId is present, a refund should be processed.
/// </summary>
public record OrderCancelledEvent : IDomainEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? CancelledBy { get; init; }

    /// <summary>
    /// Payment transaction ID if order was paid.
    /// When present, triggers automatic refund processing.
    /// Null if order was cancelled before payment or payment failed.
    /// </summary>
    public Guid? PaymentId { get; init; }

    /// <summary>
    /// Amount paid (for refund calculation). Null if no payment was made.
    /// </summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>
    /// Currency code (e.g., "USD"). Null if no payment was made.
    /// </summary>
    public string? Currency { get; init; }

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
