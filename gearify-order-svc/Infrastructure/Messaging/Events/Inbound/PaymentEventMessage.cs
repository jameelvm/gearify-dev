using System;

namespace Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Payment event message received from Payment Service via SQS.
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
    public string Provider { get; init; } = string.Empty;
    public string? ProviderTransactionId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime OccurredAt { get; init; }
}
