using System;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Domain.Entities;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record UpdateOrderStatusCommand(
    Guid OrderId,
    OrderStatus NewStatus,
    string? Reason = null,
    string? ChangedBy = null
) : IRequest<UpdateOrderStatusResult>;

public record UpdateOrderStatusResult(
    bool Success,
    OrderDto? Order = null,
    string? ErrorMessage = null
);
