using Gearify.CatalogService.Application.Queries;

namespace Gearify.CatalogService.API.Builders;

/// <summary>
/// Builder class to construct GetProductsBySlugQuery from API parameters
/// Handles merging of route-based and dropdown filter parameters
/// </summary>
public static class ProductFilterBuilder
{
    /// <summary>
    /// Builds a GetProductsBySlugQuery by merging route and dropdown filter parameters
    /// </summary>
    /// <param name="departmentSlug">Department slug from route</param>
    /// <param name="categorySlug">Category slug from route</param>
    /// <param name="subcategorySlug">Subcategory slug from route</param>
    /// <param name="brandSlug">Single brand slug from route (mega menu navigation)</param>
    /// <param name="brand">Multiple brand slugs from dropdown filter</param>
    /// <param name="priceRange">Price range string from route (format: "min-max")</param>
    /// <param name="minPrice">Min price from dropdown filter</param>
    /// <param name="maxPrice">Max price from dropdown filter</param>
    /// <param name="sortBy">Sort parameter</param>
    /// <returns>GetProductsBySlugQuery with merged filter parameters</returns>
    public static GetProductsBySlugQuery Build(
        string? departmentSlug,
        string? categorySlug,
        string? subcategorySlug,
        string? brandSlug,
        string[]? brand,
        string? priceRange,
        decimal? minPrice,
        decimal? maxPrice,
        string? sortBy)
    {
        // Merge/combine brand slugs from both route and dropdown
        var brandSlugs = MergeBrandSlugs(brandSlug, brand);

        // Determine price range: dropdown takes priority, otherwise use route
        var (finalMinPrice, finalMaxPrice) = MergePriceRange(priceRange, minPrice, maxPrice);

        return new GetProductsBySlugQuery(
            departmentSlug,
            categorySlug,
            subcategorySlug,
            brandSlugs,
            finalMinPrice,
            finalMaxPrice,
            sortBy
        );
    }

    /// <summary>
    /// Merges brand slugs from route and dropdown, removing duplicates
    /// </summary>
    private static string[]? MergeBrandSlugs(string? brandSlug, string[]? brand)
    {
        var allBrandSlugs = new List<string>();

        // Add brand from route (mega menu navigation)
        if (!string.IsNullOrEmpty(brandSlug))
            allBrandSlugs.Add(brandSlug);

        // Add brands from dropdown filter
        if (brand is { Length: > 0 })
            allBrandSlugs.AddRange(brand);

        // Deduplicate and convert to array (handles case where same brand in both route and dropdown)
        return allBrandSlugs.Count > 0
            ? allBrandSlugs.Distinct().ToArray()
            : null;
    }

    /// <summary>
    /// Merges price range from route and dropdown
    /// Dropdown takes priority; if not provided, parses route price range
    /// </summary>
    private static (decimal? minPrice, decimal? maxPrice) MergePriceRange(
        string? priceRange,
        decimal? minPrice,
        decimal? maxPrice)
    {
        // Dropdown has price selections - use them (takes priority)
        if (minPrice.HasValue || maxPrice.HasValue)
        {
            return (minPrice, maxPrice);
        }

        // No dropdown price - parse route price range (format: "min-max" e.g., "300-500")
        if (!string.IsNullOrEmpty(priceRange))
        {
            var parts = priceRange.Split('-');
            if (parts.Length == 2)
            {
                var parsedMin = decimal.TryParse(parts[0], out var min) ? min : (decimal?)null;
                var parsedMax = decimal.TryParse(parts[1], out var max) ? max : (decimal?)null;
                return (parsedMin, parsedMax);
            }
        }

        return (null, null);
    }
}
