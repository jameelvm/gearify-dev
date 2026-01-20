using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.PaymentService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using AppStripeConfiguration = Gearify.PaymentService.Infrastructure.Configuration.StripeConfiguration;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public class StripePaymentProvider : IStripePaymentProvider
{
    private readonly AppStripeConfiguration _config;
    private readonly ILogger<StripePaymentProvider> _logger;
    private readonly PaymentIntentService _paymentIntentService;
    private readonly RefundService _refundService;

    public StripePaymentProvider(
        IOptions<AppStripeConfiguration> config,
        ILogger<StripePaymentProvider> logger)
    {
        _config = config.Value;
        _logger = logger;

        // Configure Stripe with the secret key
        Stripe.StripeConfiguration.ApiKey = _config.SecretKey;

        _paymentIntentService = new PaymentIntentService();
        _refundService = new RefundService();
    }

    public async Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId)
    {
        try
        {
            _logger.LogInformation("Processing Stripe payment for order {OrderId}, amount: {Amount} {Currency}",
                orderId, amount, currency);

            // Convert amount to cents (Stripe uses smallest currency unit)
            var amountInCents = (long)(amount * 100);

            var options = new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = currency.ToLower(),
                PaymentMethod = paymentMethodToken,
                Confirm = true,
                Description = $"Order {orderId}",
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", orderId }
                },
                // Automatic payment methods allows Stripe to dynamically show payment methods
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never" // Disable redirect-based methods for API calls
                }
            };

            var paymentIntent = await _paymentIntentService.CreateAsync(options);

            if (paymentIntent.Status == "succeeded")
            {
                _logger.LogInformation("Stripe payment succeeded: {PaymentIntentId}", paymentIntent.Id);
                return (true, paymentIntent.Id);
            }

            if (paymentIntent.Status == "requires_action" || paymentIntent.Status == "requires_confirmation")
            {
                _logger.LogWarning("Stripe payment requires additional action: {Status}", paymentIntent.Status);
                return (false, paymentIntent.Id);
            }

            _logger.LogWarning("Stripe payment not successful: {Status}", paymentIntent.Status);
            return (false, paymentIntent.Id);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error during payment processing: {Code} - {Message}",
                ex.StripeError?.Code, ex.StripeError?.Message);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Stripe payment processing");
            return (false, null);
        }
    }

    public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
    {
        try
        {
            _logger.LogInformation("Processing Stripe refund for payment {TransactionId}, amount: {Amount}",
                transactionId, amount);

            var amountInCents = (long)(amount * 100);

            var options = new RefundCreateOptions
            {
                PaymentIntent = transactionId,
                Amount = amountInCents
            };

            var refund = await _refundService.CreateAsync(options);

            if (refund.Status == "succeeded")
            {
                _logger.LogInformation("Stripe refund succeeded: {RefundId}", refund.Id);
                return true;
            }

            _logger.LogWarning("Stripe refund not successful: {Status}", refund.Status);
            return false;
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error during refund: {Code} - {Message}",
                ex.StripeError?.Code, ex.StripeError?.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Stripe refund");
            return false;
        }
    }

    public async Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId)
    {
        try
        {
            return await _paymentIntentService.GetAsync(paymentIntentId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment intent: {PaymentIntentId}", paymentIntentId);
            return null;
        }
    }

    public async Task<PaymentIntent?> CancelPaymentIntentAsync(string paymentIntentId)
    {
        try
        {
            _logger.LogInformation("Cancelling payment intent: {PaymentIntentId}", paymentIntentId);
            return await _paymentIntentService.CancelAsync(paymentIntentId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Failed to cancel payment intent: {PaymentIntentId}", paymentIntentId);
            return null;
        }
    }
}
