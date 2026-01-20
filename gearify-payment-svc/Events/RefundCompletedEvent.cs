using System;

namespace Gearify.PaymentService.Events;

/// <summary>
/// Published when a refund is successfully completed.
/// </summary>
public record RefundCompletedEvent
{
    public Guid RefundId { get; init; }
    public Guid OriginalTransactionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string ProviderRefundId { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
