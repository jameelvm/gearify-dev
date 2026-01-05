namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Product entity with full catalog hierarchy support
///
/// Phase 2 Implementation Notes:
/// - CategorySlug, SubcategorySlug, and BrandSlug fields have been added to the schema
/// - These slugs enable direct product filtering without needing to look up IDs
/// - When creating/updating products, populate these slug fields from the catalog hierarchy
/// - Update GetProductsBySlugQueryHandler to use these new fields for more efficient filtering
/// - Migrate existing products to populate slug fields from their referenced entities
/// </summary>
public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // Catalog hierarchy fields
    public string Department { get; set; } = string.Empty; // e.g., "Cricket", "Perfume"
    public string DepartmentSlug { get; set; } = string.Empty; // e.g., "cricket", "perfume"
    public string Category { get; set; } = string.Empty; // e.g., "Cricket Bats"
    public string CategorySlug { get; set; } = string.Empty; // e.g., "cricket-bats"
    public string Subcategory { get; set; } = string.Empty; // e.g., "English Willow Bats"
    public string SubcategorySlug { get; set; } = string.Empty; // e.g., "english-willow"

    // Brand fields
    public string Brand { get; set; } = string.Empty; // e.g., "SS"
    public string BrandSlug { get; set; } = string.Empty; // e.g., "ss"
    // Pricing fields
    public decimal Price { get; set; }                    // Current selling price (e.g., 79.99)
    public decimal CompareAtPrice { get; set; }           // Original price for comparison (e.g., 99.99)
    public decimal? DiscountPercentage { get; set; }      // Discount % (e.g., 20 for "20% off")
    public string? OfferBadge { get; set; }               // Offer label (e.g., "SALE", "20% OFF", "HOT DEAL")
    public string Currency { get; set; } = "USD";
    public List<string> ImageUrls { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();

    // Rating fields
    public decimal? RatingAverage { get; set; }    // e.g., 4.5
    public int? RatingCount { get; set; }          // e.g., 127 reviews

    // Primary image URL for list views (thumbnail variant for performance)
    public string? ThumbnailUrl { get; set; }

    // Common product classification attributes (optimized for frequent queries)
    // Used by most tenants - indexed at top level for performance
    public bool IsDeal { get; set; } = false;              // Product is on deal/promotion
    public bool IsClearance { get; set; } = false;         // Product is on clearance
    public bool IsNewArrival { get; set; } = false;        // Recently added product
    public bool IsBestSeller { get; set; } = false;        // Popular/best-selling product
    public bool IsFeatured { get; set; } = false;          // Featured/highlighted product
    public DateTime? DealStartDate { get; set; }           // When deal starts (null = no deal)
    public DateTime? DealEndDate { get; set; }             // When deal expires (null = indefinite)

    // Tenant-specific custom collections (flexible for unique business needs)
    // Examples: "organic", "vegan", "eco-friendly", "halal", "gift-set", "limited-edition"
    public Dictionary<string, bool> CustomCollections { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}
