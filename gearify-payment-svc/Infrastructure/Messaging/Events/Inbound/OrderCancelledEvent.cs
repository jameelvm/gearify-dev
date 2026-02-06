using System;

namespace Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event received when an order is cancelled and may need a refund.
/// Published by Order Service to gearify-order-cancelled-queue.
///
/// If PaymentId is present, the order was paid and a refund should be processed.
/// If PaymentId is null, no payment was made and no action is needed.
/// </summary>
public record OrderCancelledEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? CancelledBy { get; init; }

    /// <summary>
    /// Payment transaction ID if order was paid. Null if no payment was made.
    /// </summary>
    public Guid? PaymentId { get; init; }

    /// <summary>
    /// Amount paid (for refund). Null if no payment was made.
    /// </summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>
    /// Currency code. Null if no payment was made.
    /// </summary>
    public string? Currency { get; init; }

    public DateTime OccurredAt { get; init; }
}
