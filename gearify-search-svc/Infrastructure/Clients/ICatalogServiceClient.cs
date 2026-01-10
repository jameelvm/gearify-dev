using Gearify.SearchService.Infrastructure.Clients.DTOs;

namespace Gearify.SearchService.Infrastructure.Clients;

public interface ICatalogServiceClient
{
    /// <summary>
    /// Fetch all products from the Catalog Service for a given tenant
    /// </summary>
    Task<List<CatalogProductDto>> GetAllProductsAsync(string tenantId, CancellationToken cancellationToken = default);
}
