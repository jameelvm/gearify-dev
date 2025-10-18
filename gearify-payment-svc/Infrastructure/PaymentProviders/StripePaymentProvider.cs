using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public class StripePaymentProvider : IStripePaymentProvider
{
    private readonly string _apiKey;
    private readonly HttpClient _httpClient;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<StripePaymentProvider> logger)
    {
        _apiKey = configuration["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe API key not configured");
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.stripe.com/v1/");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public async Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId)
    {
        try
        {
            _logger.LogInformation("Processing Stripe payment for order {OrderId}, amount: {Amount}", orderId, amount);

            // Convert amount to cents (Stripe uses smallest currency unit)
            var amountInCents = (int)(amount * 100);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("amount", amountInCents.ToString()),
                new KeyValuePair<string, string>("currency", currency.ToLower()),
                new KeyValuePair<string, string>("payment_method", paymentMethodToken),
                new KeyValuePair<string, string>("confirm", "true"),
                new KeyValuePair<string, string>("description", $"Order {orderId}")
            });

            var response = await _httpClient.PostAsync("payment_intents", content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                // Parse response to get payment intent ID
                var paymentIntentId = ExtractPaymentIntentId(responseBody);

                _logger.LogInformation("Stripe payment succeeded: {PaymentIntentId}", paymentIntentId);
                return (true, paymentIntentId);
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Stripe payment failed: {Error}", error);
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
            var amountInCents = (int)(amount * 100);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("payment_intent", transactionId),
                new KeyValuePair<string, string>("amount", amountInCents.ToString())
            });

            var response = await _httpClient.PostAsync("refunds", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Stripe refund");
            return false;
        }
    }

    private string? ExtractPaymentIntentId(string responseBody)
    {
        // Simple extraction - in production, use proper JSON deserialization
        var idStart = responseBody.IndexOf("\"id\": \"pi_");
        if (idStart == -1) return null;

        var idValueStart = responseBody.IndexOf("\"pi_", idStart);
        var idValueEnd = responseBody.IndexOf("\"", idValueStart + 1);

        return responseBody.Substring(idValueStart + 1, idValueEnd - idValueStart - 1);
    }
}
