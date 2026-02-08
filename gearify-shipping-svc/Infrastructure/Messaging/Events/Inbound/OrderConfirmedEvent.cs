using System;

namespace Gearify.ShippingService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Inbound event from Order Service when order is confirmed after payment.
/// </summary>
public record OrderConfirmedEvent
{
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public Guid PaymentTransactionId { get; init; }
    public OrderAddressInfo? ShippingAddress { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

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
