namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Represents an item/subcategory within a section (e.g., "English Willow", "SS", "Professional")
/// </summary>
public class Subcategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CategoryId { get; set; } = string.Empty;
    public string SectionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? BrandId { get; set; }  // Reference to Brand (if FilterType = BRAND)
    public string? PriceRangeId { get; set; }  // Reference to PriceRange (if FilterType = PRICE_RANGE)
    public string? FilterType { get; set; }  // BRAND, PRICE_RANGE, etc.
    public decimal? MinPrice { get; set; }  // Populated by PriceRangeSectionMapper
    public decimal? MaxPrice { get; set; }  // Populated by PriceRangeSectionMapper
    public int DisplayOrder { get; set; }
    public int ProductCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}
