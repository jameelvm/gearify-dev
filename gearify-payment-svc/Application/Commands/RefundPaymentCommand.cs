using System;
using Gearify.PaymentService.Application.DTOs;
using MediatR;

namespace Gearify.PaymentService.Application.Commands;

public record RefundPaymentCommand(
    Guid TransactionId,
    string TenantId,
    decimal Amount,
    string Reason,
    Guid? OrderId = null,
    string? OrderNumber = null
) : IRequest<RefundPaymentResult>;

public record RefundPaymentResult(
    bool Success,
    RefundDto? Refund = null,
    string? ErrorMessage = null
);
