using System;

namespace Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event received when a new order is created and payment needs to be processed.
/// Published by Order Service to gearify-order-created-queue.
/// </summary>
public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime OccurredAt { get; init; }
}
