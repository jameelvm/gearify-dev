namespace Gearify.SearchService.Domain.Entities;

public class ProductSearchDocument
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? DepartmentSlug { get; set; }
    public string? Category { get; set; }
    public string? CategorySlug { get; set; }
    public string? Subcategory { get; set; }
    public string? SubcategorySlug { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }
    public string? ThumbnailUrl { get; set; }
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public bool IsDeal { get; set; }
    public bool IsClearance { get; set; }
    public bool IsNewArrival { get; set; }
    public bool IsBestSeller { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
