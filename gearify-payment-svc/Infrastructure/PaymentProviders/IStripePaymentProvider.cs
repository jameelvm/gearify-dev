using System;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Commands;
using Stripe;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public interface IStripePaymentProvider
{
    Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId
    );

    Task<bool> RefundPaymentAsync(string transactionId, decimal amount);

    Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId);

    Task<PaymentIntent?> CancelPaymentIntentAsync(string paymentIntentId);
}

public interface IPayPalPaymentProvider
{
    Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId
    );

    Task<bool> RefundPaymentAsync(string transactionId, decimal amount);
}

public interface IIdempotencyService
{
    Task<ProcessPaymentResult?> GetResultAsync(string key);
    Task SaveResultAsync(string key, ProcessPaymentResult result, TimeSpan expiration);
}
