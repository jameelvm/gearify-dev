using Gearify.SearchService.Application.Services;
using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Gearify.SearchService.Infrastructure.OpenSearch;

public class ProductIndexService : IProductIndexService
{
    private readonly IElasticClient _client;
    private readonly IIndexManager _indexManager;
    private readonly OpenSearchSettings _settings;
    private readonly ILogger<ProductIndexService> _logger;
    private const int BulkBatchSize = 1000;

    public ProductIndexService(
        IOpenSearchClientFactory clientFactory,
        IIndexManager indexManager,
        IOptions<OpenSearchSettings> settings,
        ILogger<ProductIndexService> logger)
    {
        _client = clientFactory.CreateClient();
        _indexManager = indexManager;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> IndexProductAsync(ProductSearchDocument product, CancellationToken cancellationToken = default)
    {
        // Ensure index exists before indexing
        await _indexManager.EnsureProductIndexExistsAsync(product.TenantId, cancellationToken);

        var indexName = _indexManager.GetIndexName(product.TenantId);

        _logger.LogDebug("Indexing product {ProductId} to index {IndexName}", product.Id, indexName);

        var response = await _client.IndexAsync(product, idx => idx
            .Index(indexName)
            .Id(product.Id),
            cancellationToken
        );

        if (!response.IsValid)
        {
            _logger.LogError("Failed to index product {ProductId}: {Error}",
                product.Id, response.DebugInformation);
            return false;
        }

        _logger.LogInformation("Successfully indexed product {ProductId} to {IndexName}",
            product.Id, indexName);
        return true;
    }

    public async Task<bool> UpdateProductAsync(ProductSearchDocument product, CancellationToken cancellationToken = default)
    {
        // Update is same as index (upsert behavior)
        return await IndexProductAsync(product, cancellationToken);
    }

    public async Task<bool> DeleteProductAsync(string productId, string tenantId, CancellationToken cancellationToken = default)
    {
        var indexName = _indexManager.GetIndexName(tenantId);

        _logger.LogDebug("Deleting product {ProductId} from index {IndexName}", productId, indexName);

        var response = await _client.DeleteAsync<ProductSearchDocument>(productId, d => d
            .Index(indexName),
            cancellationToken
        );

        if (!response.IsValid)
        {
            // Not found is okay - product might not exist
            if (response.Result == Result.NotFound)
            {
                _logger.LogDebug("Product {ProductId} not found in index {IndexName}", productId, indexName);
                return true;
            }

            _logger.LogError("Failed to delete product {ProductId}: {Error}",
                productId, response.DebugInformation);
            return false;
        }

        _logger.LogInformation("Successfully deleted product {ProductId} from {IndexName}",
            productId, indexName);
        return true;
    }

    public async Task<BulkIndexResult> BulkIndexProductsAsync(
        IEnumerable<ProductSearchDocument> products,
        CancellationToken cancellationToken = default)
    {
        var result = new BulkIndexResult();
        var productList = products.ToList();
        result.TotalDocuments = productList.Count;

        if (productList.Count == 0)
        {
            _logger.LogDebug("No products to index");
            return result;
        }

        // Group by tenant for proper index routing
        var productsByTenant = productList.GroupBy(p => p.TenantId);

        foreach (var tenantGroup in productsByTenant)
        {
            var tenantId = tenantGroup.Key;
            var tenantProducts = tenantGroup.ToList();

            // Ensure index exists for this tenant
            await _indexManager.EnsureProductIndexExistsAsync(tenantId, cancellationToken);
            var indexName = _indexManager.GetIndexName(tenantId);

            _logger.LogInformation("Bulk indexing {Count} products for tenant {TenantId}",
                tenantProducts.Count, tenantId);

            // Process in batches
            for (int i = 0; i < tenantProducts.Count; i += BulkBatchSize)
            {
                var batch = tenantProducts.Skip(i).Take(BulkBatchSize).ToList();

                var bulkResponse = await _client.BulkAsync(b => b
                    .Index(indexName)
                    .IndexMany(batch, (descriptor, product) => descriptor.Id(product.Id)),
                    cancellationToken
                );

                if (bulkResponse.Errors)
                {
                    foreach (var item in bulkResponse.ItemsWithErrors)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Product {item.Id}: {item.Error?.Reason}");
                        _logger.LogError("Failed to index product {ProductId}: {Error}",
                            item.Id, item.Error?.Reason);
                    }
                    result.SuccessCount += batch.Count - bulkResponse.ItemsWithErrors.Count();
                }
                else
                {
                    result.SuccessCount += batch.Count;
                }

                _logger.LogDebug("Bulk indexed batch {BatchStart}-{BatchEnd} of {Total}",
                    i + 1, Math.Min(i + BulkBatchSize, tenantProducts.Count), tenantProducts.Count);
            }
        }

        _logger.LogInformation("Bulk indexing complete: {Success} succeeded, {Failed} failed",
            result.SuccessCount, result.FailedCount);

        return result;
    }
}
