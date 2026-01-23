using System;
using Gearify.PaymentService.Domain.Entities;
using MediatR;

namespace Gearify.PaymentService.Application.Commands;

/// <summary>
/// Command to process payment for an order received via event.
/// Used by the SQS consumer when receiving OrderCreatedEvent.
/// </summary>
public record ProcessOrderPaymentCommand(
    Guid OrderId,
    string OrderNumber,
    string TenantId,
    string UserId,
    decimal Amount,
    string Currency
) : IRequest<ProcessOrderPaymentResult>;

public record ProcessOrderPaymentResult(
    bool Success,
    Guid? TransactionId = null,
    string? ProviderTransactionId = null,
    PaymentErrorCode? ErrorCode = null,
    string? ErrorMessage = null
);
