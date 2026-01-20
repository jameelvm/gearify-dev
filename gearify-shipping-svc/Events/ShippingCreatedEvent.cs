using System;

namespace Gearify.ShippingService.Events;

/// <summary>
/// Published when a shipment is created for an order.
/// </summary>
public record ShippingCreatedEvent
{
    public Guid ShipmentId { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public ShippingAddressInfo ShippingAddress { get; init; } = new();
    public string Carrier { get; init; } = string.Empty;
    public string ServiceLevel { get; init; } = string.Empty;
    public DateTime? EstimatedDelivery { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public record ShippingAddressInfo
    {
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
