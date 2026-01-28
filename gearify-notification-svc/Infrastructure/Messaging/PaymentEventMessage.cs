using System;

namespace Gearify.NotificationService.Infrastructure.Messaging;

/// <summary>
/// Payment event message received from Payment Service via SQS.
/// Used for both PaymentCompletedEvent and PaymentFailedEvent.
/// </summary>
public record PaymentEventMessage
{
    public string EventType { get; init; } = string.Empty;
    public Guid TransactionId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public int Provider { get; init; }
    public string ProviderTransactionId { get; init; } = string.Empty;
    public int? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime OccurredAt { get; init; }
}
