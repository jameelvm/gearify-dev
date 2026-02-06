using System;

namespace Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event received when a refund is completed successfully.
/// Published by Payment Service to gearify-refund-completed-queue.
/// </summary>
public record RefundCompletedEvent
{
    public Guid RefundId { get; init; }
    public Guid TransactionId { get; init; }
    public Guid OriginalTransactionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string? RefundReason { get; init; }
    public string? ProviderRefundId { get; init; }
    public DateTime OccurredAt { get; init; }
}
