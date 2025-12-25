using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface IBrandRepository
{
    // Get all brands ordered by name
    Task<List<Brand>> GetAllBrandsAsync(string tenantId);

    // Get brand by ID
    Task<Brand?> GetBrandByIdAsync(string brandId, string tenantId);

    // Get brand by slug (for SEO-friendly URLs)
    Task<Brand?> GetBrandBySlugAsync(string slug, string tenantId);
}
