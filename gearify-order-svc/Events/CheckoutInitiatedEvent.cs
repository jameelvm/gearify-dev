using System;
using System.Collections.Generic;

namespace Gearify.OrderService.Events;

/// <summary>
/// Published when a customer initiates checkout from their cart.
/// Triggers order creation and payment processing.
/// </summary>
public record CheckoutInitiatedEvent
{
    public Guid CheckoutId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string? GuestId { get; init; }
    public string CartId { get; init; } = string.Empty;
    public List<CheckoutItem> Items { get; init; } = new();
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = "USD";
    public CheckoutAddress ShippingAddress { get; init; } = new();
    public CheckoutAddress? BillingAddress { get; init; }
    public CheckoutPaymentInfo PaymentInfo { get; init; } = new();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public record CheckoutItem
    {
        public string ProductId { get; init; } = string.Empty;
        public string ProductName { get; init; } = string.Empty;
        public string? VariantId { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalPrice { get; init; }
    }

    public record CheckoutAddress
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

    public record CheckoutPaymentInfo
    {
        public string Provider { get; init; } = "Stripe";
        public string PaymentMethodToken { get; init; } = string.Empty;
        public string IdempotencyKey { get; init; } = string.Empty;
    }
}
