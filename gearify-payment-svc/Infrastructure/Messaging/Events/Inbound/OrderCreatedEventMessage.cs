using System;
using System.Collections.Generic;

namespace Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Order created event message received from Order Service via SQS.
/// </summary>
public record OrderCreatedEventMessage
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public List<OrderItemInfo> Items { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "USD";
    public DateTime OccurredAt { get; init; }
}

public record OrderItemInfo
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string? VariantId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalPrice { get; init; }
}
