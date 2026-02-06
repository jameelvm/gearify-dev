using System;

namespace Gearify.NotificationService.Infrastructure.Messaging;

/// <summary>
/// Refund event message received from Payment Service via SQS.
/// Handles both RefundCompletedEvent and RefundFailedEvent.
/// </summary>
public record RefundEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Unique refund identifier (present in RefundCompletedEvent)</summary>
    public Guid? RefundId { get; init; }

    /// <summary>Original payment transaction ID</summary>
    public Guid TransactionId { get; init; }

    /// <summary>Order ID associated with this refund</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who owns the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Amount refunded (RefundCompletedEvent) or attempted (RefundFailedEvent)</summary>
    public decimal RefundAmount { get; init; }

    /// <summary>Original payment amount</summary>
    public decimal OriginalAmount { get; init; }

    /// <summary>Currency code (e.g., "USD")</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Reason for refund</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Payment provider's refund ID (e.g., Stripe refund ID)</summary>
    public string? ProviderRefundId { get; init; }

    /// <summary>Error code (present in RefundFailedEvent)</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Error message (present in RefundFailedEvent)</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Retry count (present in RefundFailedEvent)</summary>
    public int? RetryCount { get; init; }

    /// <summary>When the event occurred</summary>
    public DateTime OccurredAt { get; init; }

    // Alias for Amount field compatibility
    public decimal Amount => RefundAmount > 0 ? RefundAmount : OriginalAmount;
}
