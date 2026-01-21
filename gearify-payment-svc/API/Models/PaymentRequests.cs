using System.ComponentModel.DataAnnotations;

namespace Gearify.PaymentService.API.Models;

public record ProcessPaymentRequest
{
    [Required]
    public string OrderId { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "USD";

    [Required]
    public string Provider { get; init; } = string.Empty;

    [Required]
    public string PaymentMethodToken { get; init; } = string.Empty;

    [Required]
    public string IdempotencyKey { get; init; } = string.Empty;
}

public record ProcessRefundRequest
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; init; }

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; init; } = string.Empty;
}
