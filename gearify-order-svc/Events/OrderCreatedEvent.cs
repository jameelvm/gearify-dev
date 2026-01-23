using System;
using System.Collections.Generic;
using Gearify.SharedKernel.Events;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when an order is created from checkout.
/// </summary>
public record OrderCreatedEvent : IDomainEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public Guid CheckoutId { get; init; }
    public List<OrderItemInfo> Items { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "USD";
    public OrderAddressInfo ShippingAddress { get; init; } = new();
    public OrderAddressInfo? BillingAddress { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public record OrderItemInfo
    {
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public string? VariantId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
    }

    public record OrderAddressInfo
    {
        public string? AddressId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Street { get; init; } = string.Empty;
        public string? Street2 { get; init; }
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public string? Phone { get; init; }
    }
}
