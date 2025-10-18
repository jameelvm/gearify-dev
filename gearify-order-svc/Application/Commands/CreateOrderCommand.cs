using System.Collections.Generic;
using Gearify.OrderService.Domain.Entities;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record CreateOrderCommand(
    string TenantId,
    string UserId,
    List<OrderItem> Items,
    string ShippingAddress
) : IRequest<CreateOrderResult>;

public record CreateOrderResult(bool Success, string? OrderId = null, string? ErrorMessage = null);
