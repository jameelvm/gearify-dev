using Gearify.SearchService.Infrastructure.Configuration;

namespace Gearify.SearchService.Infrastructure.OpenSearch;

public interface IIndexManager
{
    Task<bool> CreateProductIndexAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteIndexAsync(string tenantId, string indexType, CancellationToken cancellationToken = default);
    Task<bool> IndexExistsAsync(string tenantId, string indexType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates product index if it doesn't exist. Safe to call multiple times.
    /// </summary>
    Task EnsureProductIndexExistsAsync(string tenantId, CancellationToken cancellationToken = default);

    string GetIndexName(string tenantId, string indexType = IndexNames.Products);
}
