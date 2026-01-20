using System.Collections.Generic;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record CreateOrderCommand(
    string UserId,
    List<CreateOrderItemRequest> Items,
    AddressDto ShippingAddress,
    AddressDto? BillingAddress,
    decimal Subtotal,
    decimal TaxAmount,
    decimal ShippingAmount,
    decimal DiscountAmount,
    string Currency = "USD"
) : IRequest<CreateOrderResult>;

public record CreateOrderResult(
    bool Success,
    OrderDto? Order = null,
    string? ErrorMessage = null
);
