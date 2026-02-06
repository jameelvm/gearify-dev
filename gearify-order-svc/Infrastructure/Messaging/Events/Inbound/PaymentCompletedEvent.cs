using System;

namespace Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event received when payment is completed successfully.
/// Published by Payment Service to gearify-payment-completed-queue.
/// </summary>
public record PaymentCompletedEvent
{
    public Guid TransactionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string Provider { get; init; } = string.Empty;
    public string? ProviderTransactionId { get; init; }
    public DateTime OccurredAt { get; init; }
}
