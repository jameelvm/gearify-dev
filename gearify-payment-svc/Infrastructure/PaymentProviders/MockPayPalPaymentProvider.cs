using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

/// <summary>
/// Mock PayPal provider for local development and testing.
/// Simulates payment processing without calling real PayPal APIs.
///
/// Test behaviors:
/// - Token containing "success": Always succeeds
/// - Token containing "fail": Always fails
/// - Token containing "slow": Simulates network delay (3 seconds)
/// - Any other: 90% success rate (random)
/// </summary>
public class MockPayPalPaymentProvider : IPayPalPaymentProvider
{
    private readonly ILogger<MockPayPalPaymentProvider> _logger;
    private readonly Random _random = new();

    public MockPayPalPaymentProvider(ILogger<MockPayPalPaymentProvider> logger)
    {
        _logger = logger;
    }

    public async Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId)
    {
        _logger.LogInformation("[MOCK PAYPAL] Processing payment for order {OrderId}, amount: {Amount} {Currency}, token: {Token}",
            orderId, amount, currency, paymentMethodToken);

        // Simulate API latency
        await Task.Delay(GetSimulatedDelay(paymentMethodToken));

        var transactionId = $"PAYPAL-MOCK-{Guid.NewGuid():N}".ToUpper()[..30];
        var success = ShouldSucceed(paymentMethodToken);

        if (success)
        {
            _logger.LogInformation("[MOCK PAYPAL] Payment SUCCEEDED - TransactionId: {TransactionId}", transactionId);
            return (true, transactionId);
        }
        else
        {
            _logger.LogWarning("[MOCK PAYPAL] Payment FAILED - Simulated decline for token: {Token}", paymentMethodToken);
            return (false, transactionId);
        }
    }

    public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
    {
        _logger.LogInformation("[MOCK PAYPAL] Processing refund for {TransactionId}, amount: {Amount}",
            transactionId, amount);

        // Simulate API latency
        await Task.Delay(200 + _random.Next(300));

        // Refunds succeed unless transaction ID contains "fail"
        var success = !transactionId.Contains("fail", StringComparison.OrdinalIgnoreCase);

        if (success)
        {
            _logger.LogInformation("[MOCK PAYPAL] Refund SUCCEEDED for {TransactionId}", transactionId);
        }
        else
        {
            _logger.LogWarning("[MOCK PAYPAL] Refund FAILED for {TransactionId}", transactionId);
        }

        return success;
    }

    private bool ShouldSucceed(string paymentMethodToken)
    {
        if (string.IsNullOrEmpty(paymentMethodToken))
            return true;

        var tokenLower = paymentMethodToken.ToLower();

        // Explicit test behaviors
        if (tokenLower.Contains("success")) return true;
        if (tokenLower.Contains("fail")) return false;

        // Default: 90% success rate
        return _random.Next(100) < 90;
    }

    private int GetSimulatedDelay(string paymentMethodToken)
    {
        // Token containing "slow" simulates slow network
        if (paymentMethodToken?.ToLower().Contains("slow") == true)
            return 3000;

        // Normal delay: 150-600ms (PayPal is typically slower than Stripe)
        return 150 + _random.Next(450);
    }
}
