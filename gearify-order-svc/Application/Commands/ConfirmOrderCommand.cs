using System;
using Gearify.OrderService.Application.DTOs;
using MediatR;

namespace Gearify.OrderService.Application.Commands;

public record ConfirmOrderCommand(
    Guid OrderId,
    string TenantId,
    Guid PaymentTransactionId
) : IRequest<ConfirmOrderResult>;

public record ConfirmOrderResult(
    bool Success,
    OrderDto? Order = null,
    string? ErrorMessage = null
);
