using System;
using Gearify.SharedKernel.Events;

namespace Gearify.PaymentService.Events;

/// <summary>
/// Published when a refund is successfully completed.
/// Consumed by Order Service (to update order status to Refunded)
/// and Notification Service (to send refund confirmation email).
/// </summary>
public record RefundCompletedEvent : IDomainEvent
{
    /// <summary>Tenant identifier for multi-tenancy</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Unique refund identifier</summary>
    public Guid RefundId { get; init; }

    /// <summary>Original payment transaction ID</summary>
    public Guid OriginalTransactionId { get; init; }

    /// <summary>Order ID associated with this refund</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who owns the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Amount refunded</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>Original payment amount</summary>
    public decimal OriginalAmount { get; init; }

    /// <summary>Currency code (e.g., "USD")</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Reason for refund</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Payment provider's refund ID (e.g., Stripe refund ID)</summary>
    public string? ProviderRefundId { get; init; }

    /// <summary>When the refund was completed</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    // Legacy field for backwards compatibility
    [Obsolete("Use RefundAmount instead")]
    public decimal Amount => RefundAmount;
}
