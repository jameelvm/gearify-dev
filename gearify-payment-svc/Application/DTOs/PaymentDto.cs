using System;
using System.Collections.Generic;

namespace Gearify.PaymentService.Application.DTOs;

public record PaymentDto
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string Status { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string? ProviderTransactionId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ErrorMessage { get; init; }
    public List<RefundDto> Refunds { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record PaymentSummaryDto
{
    public Guid Id { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string Status { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public record RefundDto
{
    public Guid Id { get; init; }
    public Guid TransactionId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "USD";
    public string Status { get; init; } = string.Empty;
    public string? ProviderRefundId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public record PaymentListDto
{
    public List<PaymentSummaryDto> Payments { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}
