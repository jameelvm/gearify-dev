using Gearify.PaymentService.Domain.Entities;
using MediatR;

namespace Gearify.PaymentService.Application.Commands;

public record ProcessPaymentCommand(
    string TenantId,
    string OrderId,
    string UserId,
    decimal Amount,
    string Currency,
    PaymentProvider Provider,
    string PaymentMethodToken,
    string IdempotencyKey
) : IRequest<ProcessPaymentResult>;

public record ProcessPaymentResult(
    bool Success,
    Guid? TransactionId = null,
    PaymentStatus? Status = null,
    string? ErrorMessage = null
);
