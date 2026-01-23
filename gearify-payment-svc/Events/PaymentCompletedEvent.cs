using System;
using Gearify.PaymentService.Domain.Entities;
using Gearify.SharedKernel.Events;

namespace Gearify.PaymentService.Events;

/// <summary>
/// Published when payment is successfully completed.
/// Triggers order confirmation and shipping creation.
/// </summary>
public record PaymentCompletedEvent : IDomainEvent
{
    public Guid TransactionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public PaymentProvider Provider { get; init; }
    public string ProviderTransactionId { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
