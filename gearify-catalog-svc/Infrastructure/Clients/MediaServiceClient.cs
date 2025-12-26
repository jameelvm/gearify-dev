using System.Net.Http.Headers;
using System.Text.Json;
using Gearify.CatalogService.Infrastructure.Clients.DTOs;
using Gearify.SharedKernel.Multitenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Gearify.CatalogService.Infrastructure.Clients;

/// <summary>
/// HTTP client for Media Service with resilience policies
/// </summary>
public class MediaServiceClient : IMediaServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MediaServiceClient> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
    private readonly JsonSerializerOptions _jsonOptions;

    public MediaServiceClient(
        HttpClient httpClient,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<MediaServiceClient> logger)
    {
        _httpClient = httpClient;
        _tenantContext = tenantContext;
        _logger = logger;

        // Configure base URL
        var baseUrl = configuration["MediaService:BaseUrl"] ?? "http://localhost:5009";
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
                        "Media Service call failed. Retry {RetryCount} after {DelaySeconds}s",
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
                        "Media Service circuit breaker opened for {DurationSeconds}s",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Media Service circuit breaker reset");
                });
    }

    public async Task<MediaUploadResponse?> UploadProductImageAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        long sizeInBytes,
        string productId,
        int displayOrder = 0,
        string? altText = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    using var content = new MultipartFormDataContent();

                    // Add file
                    var streamContent = new StreamContent(imageStream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                    content.Add(streamContent, "file", fileName);

                    // Add form fields
                    content.Add(new StringContent("Product"), "entityType");
                    content.Add(new StringContent(productId), "entityId");
                    content.Add(new StringContent(displayOrder.ToString()), "displayOrder");

                    if (!string.IsNullOrEmpty(altText))
                    {
                        content.Add(new StringContent(altText), "altText");
                    }

                    // Add tenant header
                    var request = new HttpRequestMessage(HttpMethod.Post, "api/media/upload");
                    request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId);
                    request.Content = content;

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogError(
                            "Failed to upload image to Media Service. Status: {StatusCode}, Error: {Error}",
                            response.StatusCode,
                            error);
                        return null;
                    }

                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var result = JsonSerializer.Deserialize<MediaUploadResponse>(json, _jsonOptions);

                    _logger.LogInformation(
                        "Successfully uploaded image {FileName} for product {ProductId}. MediaId: {MediaId}",
                        fileName,
                        productId,
                        result?.MediaId);

                    return result;
                });
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Media Service circuit breaker is open. Image upload skipped.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Media Service");
            return null;
        }
    }

    public async Task<List<MediaMetadataDto>> GetProductImagesAsync(
        string productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"api/media/Product/{productId}");
                    request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Failed to get images for product {ProductId}. Status: {StatusCode}",
                            productId,
                            response.StatusCode);
                        return new List<MediaMetadataDto>();
                    }

                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var images = JsonSerializer.Deserialize<List<MediaMetadataDto>>(json, _jsonOptions);

                    return images ?? new List<MediaMetadataDto>();
                });
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Media Service circuit breaker is open. Returning empty image list.");
            return new List<MediaMetadataDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting images from Media Service for product {ProductId}", productId);
            return new List<MediaMetadataDto>();
        }
    }

    public async Task<bool> DeleteImageAsync(
        string mediaId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    var request = new HttpRequestMessage(
                        HttpMethod.Delete,
                        $"api/media/{mediaId}?hardDelete={hardDelete}");
                    request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId);

                    var response = await _httpClient.SendAsync(request, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Failed to delete image {MediaId}. Status: {StatusCode}",
                            mediaId,
                            response.StatusCode);
                        return false;
                    }

                    _logger.LogInformation("Successfully deleted image {MediaId}", mediaId);
                    return true;
                });
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Media Service circuit breaker is open. Image deletion skipped.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {MediaId}", mediaId);
            return false;
        }
    }

    public async Task<bool> DeleteProductImagesAsync(
        string productId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get all images for the product
            var images = await GetProductImagesAsync(productId, cancellationToken);

            if (!images.Any())
            {
                return true;
            }

            // Delete each image
            var deleteTasks = images.Select(img =>
                DeleteImageAsync(img.Id, hardDelete, cancellationToken));

            var results = await Task.WhenAll(deleteTasks);

            var allDeleted = results.All(r => r);

            if (allDeleted)
            {
                _logger.LogInformation(
                    "Successfully deleted {Count} images for product {ProductId}",
                    images.Count,
                    productId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to delete some images for product {ProductId}",
                    productId);
            }

            return allDeleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting images for product {ProductId}", productId);
            return false;
        }
    }
}
