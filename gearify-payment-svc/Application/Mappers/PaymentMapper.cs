using System.Linq;
using Gearify.PaymentService.Application.DTOs;
using Gearify.PaymentService.Domain.Entities;

namespace Gearify.PaymentService.Application.Mappers;

public static class PaymentMapper
{
    public static PaymentDto ToDto(PaymentTransaction transaction)
    {
        return new PaymentDto
        {
            Id = transaction.Id,
            TenantId = transaction.TenantId,
            OrderId = transaction.OrderId,
            UserId = transaction.UserId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status.ToString(),
            Provider = transaction.Provider.ToString(),
            ProviderTransactionId = transaction.ProviderTransactionId,
            IdempotencyKey = transaction.IdempotencyKey,
            ErrorMessage = transaction.ErrorMessage,
            Refunds = transaction.Refunds.Select(ToRefundDto).ToList(),
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt
        };
    }

    public static PaymentSummaryDto ToSummaryDto(PaymentTransaction transaction)
    {
        return new PaymentSummaryDto
        {
            Id = transaction.Id,
            OrderId = transaction.OrderId,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status.ToString(),
            Provider = transaction.Provider.ToString(),
            CreatedAt = transaction.CreatedAt
        };
    }

    public static RefundDto ToRefundDto(Refund refund)
    {
        return new RefundDto
        {
            Id = refund.Id,
            TransactionId = refund.TransactionId,
            Amount = refund.Amount,
            Currency = refund.Currency,
            Status = refund.Status.ToString(),
            ProviderRefundId = refund.ProviderRefundId,
            Reason = refund.Reason,
            CreatedAt = refund.CreatedAt,
            CompletedAt = refund.CompletedAt
        };
    }
}
