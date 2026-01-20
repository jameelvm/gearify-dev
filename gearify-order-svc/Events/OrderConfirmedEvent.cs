using System;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when an order is confirmed after successful payment.
/// </summary>
public record OrderConfirmedEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public Guid PaymentTransactionId { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
