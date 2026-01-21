using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Gearify.OrderService.Application.DTOs;

namespace Gearify.OrderService.API.Models;

public record CreateOrderRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CreateOrderItemRequest> Items { get; init; } = new();

    [Required]
    public AddressDto ShippingAddress { get; init; } = null!;

    public AddressDto? BillingAddress { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Subtotal { get; init; }

    [Range(0, double.MaxValue)]
    public decimal TaxAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal ShippingAmount { get; init; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; init; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "USD";
}

public record CancelOrderRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;

    public string? CancelledBy { get; init; }
}

public record UpdateOrderStatusRequest
{
    [Required]
    public string Status { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public string? ChangedBy { get; init; }
}

public record OrderListResponse
{
    public List<OrderSummaryDto> Orders { get; init; } = new();
    public int TotalCount { get; init; }
}
