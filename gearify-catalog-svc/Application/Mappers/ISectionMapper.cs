using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Application.Mappers;

/// <summary>
/// Strategy interface for enriching subcategories with data from external sources
/// </summary>
public interface ISectionMapper
{
    /// <summary>
    /// The mapping type this mapper handles (e.g., "BRAND", "CATEGORY")
    /// </summary>
    string MappingType { get; }

    /// <summary>
    /// Enriches subcategories with data from the mapped source
    /// For BRAND mapping: fetches brand details for subcategories that have BrandId
    /// </summary>
    /// <param name="subcategories">Existing subcategories to enrich</param>
    /// <param name="tenantId">The tenant ID for data isolation</param>
    /// <returns>Enriched subcategories with full details from the data source</returns>
    Task<List<Subcategory>> EnrichAsync(List<Subcategory> subcategories, string tenantId);
}
