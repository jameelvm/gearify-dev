using System;
using System.Collections.Generic;

namespace Gearify.PaymentService.Domain.Entities;

public class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentProvider Provider { get; set; }
    public string? ProviderTransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<PaymentLedgerEntry> LedgerEntries { get; set; } = new List<PaymentLedgerEntry>();
    public ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded,
    Cancelled
}

public enum PaymentProvider
{
    Stripe,
    PayPal
}

public class PaymentLedgerEntry
{
    public long Id { get; set; }
    public Guid TransactionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime EntryTime { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;

    // Navigation property
    public PaymentTransaction Transaction { get; set; } = null!;
}

public class Refund
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public RefundStatus Status { get; set; } = RefundStatus.Pending;
    public string? ProviderRefundId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation property
    public PaymentTransaction Transaction { get; set; } = null!;
}

public enum PaymentErrorCode
{
    None,
    PaymentFailed,
    Exception,
    InsufficientFunds,
    CardDeclined,
    ProviderError
}

public enum RefundStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}
