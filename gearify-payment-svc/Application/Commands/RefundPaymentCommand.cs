using System;
using Gearify.PaymentService.Application.DTOs;
using MediatR;

namespace Gearify.PaymentService.Application.Commands;

public record RefundPaymentCommand(
    Guid TransactionId,
    decimal Amount,
    string Reason
) : IRequest<RefundPaymentResult>;

public record RefundPaymentResult(
    bool Success,
    RefundDto? Refund = null,
    string? ErrorMessage = null
);
