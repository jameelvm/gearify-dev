using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// Repository interface for managing price range configurations
/// </summary>
public interface IPriceRangeRepository
{
    /// <summary>
    /// Get all active price ranges for a tenant
    /// </summary>
    /// <param name="tenantId">Tenant identifier</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="onlyCategorySpecific">If true, only return category-specific ranges (exclude global)</param>
    /// <returns>List of price ranges ordered by DisplayOrder</returns>
    Task<List<PriceRange>> GetPriceRangesAsync(string tenantId, string? category = null, bool onlyCategorySpecific = false);

    /// <summary>
    /// Get price range by ID
    /// </summary>
    /// <param name="id">Price range ID</param>
    /// <param name="tenantId">Tenant identifier</param>
    /// <returns>Price range or null if not found</returns>
    Task<PriceRange?> GetByIdAsync(string id, string tenantId);

    /// <summary>
    /// Create a new price range
    /// </summary>
    /// <param name="priceRange">Price range to create</param>
    Task CreateAsync(PriceRange priceRange);

    /// <summary>
    /// Update an existing price range
    /// </summary>
    /// <param name="priceRange">Price range to update</param>
    Task UpdateAsync(PriceRange priceRange);

    /// <summary>
    /// Delete a price range
    /// </summary>
    /// <param name="id">Price range ID</param>
    /// <param name="tenantId">Tenant identifier</param>
    Task DeleteAsync(string id, string tenantId);
}
