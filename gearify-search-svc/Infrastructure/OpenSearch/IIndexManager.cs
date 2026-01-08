namespace Gearify.SearchService.Infrastructure.OpenSearch;

public interface IIndexManager
{
    Task<bool> CreateIndexAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteIndexAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<bool> IndexExistsAsync(string tenantId, CancellationToken cancellationToken = default);
    string GetIndexName(string tenantId);
}
