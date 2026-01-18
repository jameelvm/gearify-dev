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

/// <summary>
/// HTTP client for Catalog Service with resilience policies
/// </summary>
public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CatalogServiceClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
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

        // Configure base URL
        var baseUrl = configuration["CatalogService:BaseUrl"] ?? "http://localhost:5001";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        // JSON serialization options
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Retry policy: 3 retries with exponential backoff
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Catalog Service call failed. Retry {RetryCount} after {DelaySeconds}s",
                        retryCount,
                        timeSpan.TotalSeconds);
                });

        // Circuit breaker: break after 5 consecutive failures, stay open for 30 seconds
        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    _logger.LogError(
                        exception,
                        "Catalog Service circuit breaker opened for {DurationSeconds}s",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Catalog Service circuit breaker reset");
                });
    }

    public async Task<ProductValidationResult?> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    // Create new request for each attempt (HttpRequestMessage cannot be reused)
                    var request = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/products/{productId}");
                    request.Headers.Add("X-Tenant-Id", tenantId);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Product {ProductId} not found in catalog service", productId);
                        return null;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError(
                            "Failed to get product from Catalog Service. Status: {StatusCode}, Error: {Error}",
                            response.StatusCode,
                            error);
                        throw new HttpRequestException($"Catalog service returned {response.StatusCode}");
                    }

                    var product = await response.Content.ReadFromJsonAsync<ProductValidationResult>(_jsonOptions, cancellationToken);

                    _logger.LogDebug("Retrieved product {ProductId} from catalog service", productId);

                    return product;
                });
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Catalog Service circuit breaker is open. Cannot retrieve product {ProductId}", productId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get product {ProductId} from catalog service", productId);
            throw;
        }
    }
}
