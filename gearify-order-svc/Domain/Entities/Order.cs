using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Gearify.OrderService.Domain.Entities;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // Amounts
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";

    // Addresses (ID reference + JSONB snapshot)
    public string? ShippingAddressId { get; set; }
    public JsonDocument? ShippingAddress { get; set; }
    public string? BillingAddressId { get; set; }
    public JsonDocument? BillingAddress { get; set; }

    // Payment reference
    public Guid? PaymentId { get; set; }
    public string? PaymentStatus { get; set; }

    // Shipping reference
    public Guid? ShipmentId { get; set; }
    public string? ShippingStatus { get; set; }

    // Saga state
    public SagaState SagaState { get; set; } = SagaState.Created;
    public string? SagaStep { get; set; }
    public string? SagaError { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

// Cancellation tracking (for deferred cancellation during PaymentProcessing)
    public string? CancellationReason { get; set; }
    public string? CancellationRequestedBy { get; set; }
    public DateTime? CancellationRequestedAt { get; set; }

    // Metadata
    public string? Notes { get; set; }
    public JsonDocument? Metadata { get; set; }

    // Navigation properties
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();

    // Helper method to generate order number
    public static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Guid.NewGuid().ToString("N")[..6].ToUpper();
        return $"ORD-{timestamp}-{random}";
    }
}

public enum OrderStatus
{
    Pending,
    PaymentProcessing,
    PaymentFailed,
    Paid,
    Processing, //Order Processing
    Shipped,    
    Delivered,
    Cancelled,
    Refunded
}

public enum SagaState
{
    Created,
    PaymentPending,
    PaymentCompleted,
    ShippingPending,
    ShippingCreated,
    Completed,
    Compensating,
    Failed
}
