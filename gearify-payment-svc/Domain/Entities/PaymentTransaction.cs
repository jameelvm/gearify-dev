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
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Refunded,
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
    public string AccountType { get; set; } = string.Empty; // "debit" or "credit"
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime EntryTime { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
}
