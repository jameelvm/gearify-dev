using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;

namespace Gearify.CatalogService.Application.Mappers;

/// <summary>
/// Enriches subcategories with brand details for sections with Mapping = "BRAND"
/// Fetches ONLY the brands referenced by BrandId in existing subcategories
/// </summary>
public class BrandSectionMapper : ISectionMapper
{
    private readonly IBrandRepository _brandRepository;

    public BrandSectionMapper(IBrandRepository brandRepository)
    {
        _brandRepository = brandRepository;
    }

    public string MappingType => "BRAND";

    public async Task<List<Subcategory>> EnrichAsync(List<Subcategory> subcategories, string tenantId)
    {
        // Extract unique BrandIds from subcategories (only those with BrandId set)
        var brandIds = subcategories
            .Where(s => !string.IsNullOrEmpty(s.BrandId))
            .Select(s => s.BrandId!)
            .Distinct()
            .ToList();

        if (!brandIds.Any())
        {
            return subcategories; // No brands to enrich, return as-is
        }

        // Fetch ONLY the brands that are referenced (not all brands)
        var brandTasks = brandIds
            .Select(id => _brandRepository.GetBrandByIdAsync(id, tenantId))
            .ToList();

        var brands = await Task.WhenAll(brandTasks);
        var brandMap = brands
            .Where(b => b != null)
            .Cast<Brand>()
            .ToDictionary(b => b.Id);

        // Enrich subcategories with brand details
        foreach (var subcategory in subcategories)
        {
            if (!string.IsNullOrEmpty(subcategory.BrandId) && brandMap.TryGetValue(subcategory.BrandId, out var brand))
            {
                // Enrich with full brand details
                subcategory.Description = brand.Description;
                subcategory.ImageUrl = brand.Logo;
                // Name and Slug are already set in DB, but we could override if needed
                subcategory.Name = brand.Name;
                subcategory.Slug = brand.Slug;
            }
        }

        return subcategories;
    }
}
