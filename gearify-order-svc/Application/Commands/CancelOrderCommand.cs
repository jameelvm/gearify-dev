using System;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record CancelOrderCommand(
    Guid OrderId,
    string Reason,
    string? CancelledBy = null
) : IRequest<CancelOrderResult>;

public record CancelOrderResult(
    bool Success,
    OrderDto? Order = null,
    string? ErrorMessage = null
);
