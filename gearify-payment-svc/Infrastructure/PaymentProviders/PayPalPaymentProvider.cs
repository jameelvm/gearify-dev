using Microsoft.Extensions.Logging;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public class PayPalPaymentProvider : IPayPalPaymentProvider
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PayPalPaymentProvider> _logger;
    private string? _accessToken;
    private DateTime _tokenExpiration;

    public PayPalPaymentProvider(
        IConfiguration configuration,
        HttpClient httpClient,
        ILogger<PayPalPaymentProvider> logger)
    {
        _clientId = configuration["PayPal:ClientId"] ?? "paypal-client-id";
        _clientSecret = configuration["PayPal:ClientSecret"] ?? "paypal-secret";
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(configuration["PayPal:BaseUrl"] ?? "https://api-m.sandbox.paypal.com/");
    }

    public async Task<(bool Success, string? TransactionId)> ProcessPaymentAsync(
        decimal amount,
        string currency,
        string paymentMethodToken,
        string orderId)
    {
        try
        {
            _logger.LogInformation("Processing PayPal payment for order {OrderId}, amount: {Amount}", orderId, amount);

            // Ensure we have a valid access token
            await EnsureAccessTokenAsync();

            // Create PayPal order
            var orderData = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        reference_id = orderId,
                        amount = new
                        {
                            currency_code = currency.ToUpper(),
                            value = amount.ToString("F2")
                        }
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "v2/checkout/orders")
            {
                Content = JsonContent.Create(orderData)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var paypalOrderId = ExtractOrderId(responseBody);

                // Capture the order
                var captureRequest = new HttpRequestMessage(HttpMethod.Post, $"v2/checkout/orders/{paypalOrderId}/capture");
                captureRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                var captureResponse = await _httpClient.SendAsync(captureRequest);

                if (captureResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation("PayPal payment succeeded: {OrderId}", paypalOrderId);
                    return (true, paypalOrderId);
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("PayPal payment failed: {Error}", error);
            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during PayPal payment processing");
            return (false, null);
        }
    }

    public async Task<bool> RefundPaymentAsync(string transactionId, decimal amount)
    {
        try
        {
            await EnsureAccessTokenAsync();

            var refundData = new
            {
                amount = new
                {
                    value = amount.ToString("F2"),
                    currency_code = "USD"
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"v2/payments/captures/{transactionId}/refund")
            {
                Content = JsonContent.Create(refundData)
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during PayPal refund");
            return false;
        }
    }

    private async Task EnsureAccessTokenAsync()
    {
        if (_accessToken != null && DateTime.UtcNow < _tokenExpiration)
            return;

        var authValue = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            })
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        _accessToken = ExtractAccessToken(responseBody);
        _tokenExpiration = DateTime.UtcNow.AddHours(1);
    }

    private string? ExtractOrderId(string responseBody)
    {
        // Simple extraction - in production, use proper JSON deserialization
        var idStart = responseBody.IndexOf("\"id\": \"");
        if (idStart == -1) return null;

        var idValueStart = idStart + 7;
        var idValueEnd = responseBody.IndexOf("\"", idValueStart);

        return responseBody.Substring(idValueStart, idValueEnd - idValueStart);
    }

    private string? ExtractAccessToken(string responseBody)
    {
        var tokenStart = responseBody.IndexOf("\"access_token\": \"");
        if (tokenStart == -1) return null;

        var tokenValueStart = tokenStart + 17;
        var tokenValueEnd = responseBody.IndexOf("\"", tokenValueStart);

        return responseBody.Substring(tokenValueStart, tokenValueEnd - tokenValueStart);
    }
}
