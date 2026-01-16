using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Infrastructure.Clients.DTOs;
using Gearify.SharedKernel.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Gearify.CartService.Infrastructure.Clients;

public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CatalogServiceClient> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy<HttpResponseMessage> _circuitBreakerPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public CatalogServiceClient(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<CatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _logger = logger;

        var baseUrl = configuration["CatalogService:BaseUrl"] ?? "http://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Retry policy: 3 retries with exponential backoff
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.NotFound)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} for catalog service after {Delay}ms. Status: {StatusCode}",
                        retryCount, timespan.TotalMilliseconds, outcome.Result?.StatusCode);
                });

        // Circuit breaker: break after 5 failures, stay open for 30 seconds
        _circuitBreakerPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && r.StatusCode != HttpStatusCode.NotFound)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                5,
                TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    _logger.LogError(
                        "Circuit breaker opened for catalog service. Will retry after {BreakDelay}s",
                        breakDelay.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for catalog service");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for catalog service");
                });
    }

    public async Task<ProductValidationResult?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/products/{productId}");
            request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId);

            var response = await _circuitBreakerPolicy
                .WrapAsync(_retryPolicy)
                .ExecuteAsync(async () => await _httpClient.SendAsync(request, cancellationToken));

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product {ProductId} not found in catalog service", productId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<ProductValidationResult>(_jsonOptions, cancellationToken);

            _logger.LogDebug("Retrieved product {ProductId} from catalog service", productId);

            return product;
        }
        catch (BrokenCircuitException)
        {
            _logger.LogError("Circuit breaker is open. Cannot reach catalog service for product {ProductId}", productId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product {ProductId} from catalog service", productId);
            throw;
        }
    }
}
