using System;
using Gearify.SharedKernel.Events;

namespace Gearify.PaymentService.Events;

/// <summary>
/// Published when a refund fails after all retry attempts.
/// Consumed by Notification Service to alert customer and admin.
/// </summary>
public record RefundFailedEvent : IDomainEvent
{
    /// <summary>Tenant identifier for multi-tenancy</summary>
    public string TenantId { get; init; } = string.Empty;

    /// <summary>Original payment transaction ID</summary>
    public Guid TransactionId { get; init; }

    /// <summary>Order ID associated with this refund attempt</summary>
    public Guid OrderId { get; init; }

    /// <summary>Human-readable order number</summary>
    public string OrderNumber { get; init; } = string.Empty;

    /// <summary>User who owns the order</summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>Amount that failed to refund</summary>
    public decimal Amount { get; init; }

    /// <summary>Currency code (e.g., "USD")</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Error code from payment provider or system</summary>
    public string ErrorCode { get; init; } = string.Empty;

    /// <summary>Human-readable error message</summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>Number of retry attempts made before failure</summary>
    public int RetryCount { get; init; }

    /// <summary>When the final failure occurred</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
