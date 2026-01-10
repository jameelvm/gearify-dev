using System.Text.Json;
using Gearify.SearchService.Infrastructure.Clients.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.Infrastructure.Clients;

public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public CatalogServiceClient(
        HttpClient httpClient,
        ILogger<CatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<CatalogProductDto>> GetAllProductsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var allProducts = new List<CatalogProductDto>();
        string? cursor = null;
        const int pageSize = 100;
        int pageNumber = 0;

        _logger.LogInformation("Starting to fetch all products from Catalog Service for tenant {TenantId}", tenantId);

        do
        {
            pageNumber++;
            var url = $"/api/catalog/products?pageSize={pageSize}";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&cursor={cursor}";
            }

            _logger.LogDebug("Fetching page {PageNumber} from Catalog Service: {Url}", pageNumber, url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Tenant-Id", tenantId);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Failed to fetch products from Catalog Service. Status: {StatusCode}, Error: {Error}",
                    response.StatusCode, errorContent);
                break;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<CatalogProductListResponse>(content, _jsonOptions);

            if (result == null)
            {
                _logger.LogWarning("Received null response from Catalog Service on page {PageNumber}", pageNumber);
                break;
            }

            if (result.Products.Count == 0)
            {
                _logger.LogDebug("No more products found on page {PageNumber}", pageNumber);
                break;
            }

            // Set tenant ID on each product
            foreach (var product in result.Products)
            {
                product.TenantId = tenantId;
            }

            allProducts.AddRange(result.Products);
            cursor = result.NextCursor;

            _logger.LogDebug(
                "Fetched {Count} products on page {PageNumber}. Total so far: {Total}. HasMore: {HasMore}",
                result.Products.Count, pageNumber, allProducts.Count, result.HasMore);

            // Stop if no more pages
            if (!result.HasMore)
                break;

        } while (!string.IsNullOrEmpty(cursor));

        _logger.LogInformation(
            "Completed fetching products from Catalog Service. Total: {TotalCount} products for tenant {TenantId}",
            allProducts.Count, tenantId);

        return allProducts;
    }
}
