using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;

namespace Gearify.CatalogService.Application.Mappers;

/// <summary>
/// Enriches subcategories with price range details for sections with Mapping = "PRICE_RANGE"
/// Fetches ONLY the price ranges referenced by PriceRangeId in existing subcategories
/// </summary>
public class PriceRangeSectionMapper : ISectionMapper
{
    private readonly IPriceRangeRepository _priceRangeRepository;

    public PriceRangeSectionMapper(IPriceRangeRepository priceRangeRepository)
    {
        _priceRangeRepository = priceRangeRepository;
    }

    public string MappingType => "PRICE_RANGE";

    public async Task<List<Subcategory>> EnrichAsync(List<Subcategory> subcategories, string tenantId)
    {
        // Extract unique PriceRangeIds from subcategories (only those with PriceRangeId set)
        var priceRangeIds = subcategories
            .Where(s => !string.IsNullOrEmpty(s.PriceRangeId))
            .Select(s => s.PriceRangeId!)
            .Distinct()
            .ToList();

        if (!priceRangeIds.Any())
        {
            return subcategories; // No price ranges to enrich, return as-is
        }

        // Fetch ONLY the price ranges that are referenced (not all price ranges)
        var priceRangeTasks = priceRangeIds
            .Select(id => _priceRangeRepository.GetByIdAsync(id, tenantId))
            .ToList();

        var priceRanges = await Task.WhenAll(priceRangeTasks);
        var priceRangeMap = priceRanges
            .Where(pr => pr != null)
            .Cast<PriceRange>()
            .ToDictionary(pr => pr.Id);

        // Enrich subcategories with price range details
        foreach (var subcategory in subcategories)
        {
            if (!string.IsNullOrEmpty(subcategory.PriceRangeId) &&
                priceRangeMap.TryGetValue(subcategory.PriceRangeId, out var priceRange))
            {
                // Enrich with price range details
                subcategory.Name = priceRange.Label;
                subcategory.Description = $"Products priced {priceRange.Label}";

                // Generate a slug from the label
                subcategory.Slug = priceRange.Label.ToLowerInvariant()
                    .Replace("$", "")
                    .Replace(" ", "-")
                    .Replace("&", "and");
            }
        }

        return subcategories;
    }
}
