using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stripe;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

/// <summary>
/// Mock Stripe provider for local development and testing.
/// Simulates payment processing without calling real Stripe APIs.
///
/// Test card behaviors:
/// - Card ending in 0000: Always succeeds
/// - Card ending in 9999: Always fails
/// - Card ending in 3333: Simulates network delay (3 seconds)
/// - Any other: 90% success rate (random)
/// </summary>
public class MockStripePaymentProvider : IStripePaymentProvider
{
    private readonly ILogger<MockStripePaymentProvider> _logger;
    private readonly Random _random = new();

    public MockStripePaymentProvider(ILogger<MockStripePaymentProvider> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId)
    {
        _logger.LogInformation("[MOCK STRIPE] Processing payment for order {OrderId}, amount: {Amount} {Currency}, token: {Token}",
            orderId, amount, currency, paymentMethodToken);

        // Simulate API latency
        await Task.Delay(GetSimulatedDelay(paymentMethodToken));

        var transactionId = $"pi_mock_{Guid.NewGuid():N}";
        var success = ShouldSucceed(paymentMethodToken);

        if (success)
        {
            _logger.LogInformation("[MOCK STRIPE] Payment SUCCEEDED - TransactionId: {TransactionId}", transactionId);
            return (true, transactionId);
        }
        else
        {
            _logger.LogWarning("[MOCK STRIPE] Payment FAILED - Simulated decline for token: {Token}", paymentMethodToken);
            return (false, transactionId);
        }
    }

    public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[MOCK STRIPE] Processing refund for {TransactionId}, amount: {Amount}",
            transactionId, amount);

        // Simulate API latency
        await Task.Delay(200 + _random.Next(300));

        // Refunds succeed unless transaction ID contains "fail"
        var success = !transactionId.Contains("fail", StringComparison.OrdinalIgnoreCase);

        if (success)
        {
            _logger.LogInformation("[MOCK STRIPE] Refund SUCCEEDED for {TransactionId}", transactionId);
        }
        else
        {
            _logger.LogWarning("[MOCK STRIPE] Refund FAILED for {TransactionId}", transactionId);
        }

        return success;
    }

    public Task<PaymentIntent?> GetPaymentIntentAsync(string paymentIntentId)
    {
        _logger.LogInformation("[MOCK STRIPE] Getting payment intent: {PaymentIntentId}", paymentIntentId);

        // Return a mock PaymentIntent - this requires Stripe SDK types
        // For testing purposes, we return null as we can't easily construct a PaymentIntent
        return Task.FromResult<PaymentIntent?>(null);
    }

    public Task<PaymentIntent?> CancelPaymentIntentAsync(string paymentIntentId)
    {
        _logger.LogInformation("[MOCK STRIPE] Cancelling payment intent: {PaymentIntentId}", paymentIntentId);
        return Task.FromResult<PaymentIntent?>(null);
    }

    private bool ShouldSucceed(string paymentMethodToken)
    {
        if (string.IsNullOrEmpty(paymentMethodToken))
            return true;

        // Test card behaviors based on token suffix
        if (paymentMethodToken.EndsWith("0000")) return true;   // Always succeed
        if (paymentMethodToken.EndsWith("9999")) return false;  // Always fail
        if (paymentMethodToken.EndsWith("4242")) return true;   // Classic test card - succeed

        // Default: 90% success rate
        return _random.Next(100) < 90;
    }

    private int GetSimulatedDelay(string paymentMethodToken)
    {
        // Token ending in 3333 simulates slow network
        if (paymentMethodToken?.EndsWith("3333") == true)
            return 3000;

        // Normal delay: 100-500ms
        return 100 + _random.Next(400);
    }
}
