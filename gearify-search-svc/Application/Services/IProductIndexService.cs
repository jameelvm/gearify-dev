using Gearify.SearchService.Domain.Entities;

namespace Gearify.SearchService.Application.Services;

public interface IProductIndexService
{
    /// <summary>
    /// Index a single product
    /// </summary>
    Task<bool> IndexProductAsync(ProductSearchDocument product, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk index multiple products (batch size: 1000)
    /// </summary>
    Task<BulkIndexResult> BulkIndexProductsAsync(IEnumerable<ProductSearchDocument> products, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a product from the index
    /// </summary>
    Task<bool> DeleteProductAsync(string productId, string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a product in the index (upsert)
    /// </summary>
    Task<bool> UpdateProductAsync(ProductSearchDocument product, CancellationToken cancellationToken = default);
}

public class BulkIndexResult
{
    public int TotalDocuments { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsSuccess => FailedCount == 0;
}
