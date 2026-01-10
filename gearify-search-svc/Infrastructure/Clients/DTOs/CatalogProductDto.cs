using System.Text.Json.Serialization;

namespace Gearify.SearchService.Infrastructure.Clients.DTOs;

/// <summary>
/// DTO representing a product from the Catalog Service API (ProductListDto format)
/// Maps to the lightweight product list view from the catalog service.
/// </summary>
public class CatalogProductDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    // Set by the client after fetching
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("brandSlug")]
    public string BrandSlug { get; set; } = string.Empty;

    // Pricing
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("compareAtPrice")]
    public decimal CompareAtPrice { get; set; }

    [JsonPropertyName("discountPercentage")]
    public decimal? DiscountPercentage { get; set; }

    [JsonPropertyName("offerBadge")]
    public string? OfferBadge { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    // Ratings
    [JsonPropertyName("ratingAverage")]
    public decimal? RatingAverage { get; set; }

    [JsonPropertyName("ratingCount")]
    public int? RatingCount { get; set; }

    // Catalog hierarchy
    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("departmentSlug")]
    public string DepartmentSlug { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("categorySlug")]
    public string CategorySlug { get; set; } = string.Empty;

    [JsonPropertyName("subcategory")]
    public string Subcategory { get; set; } = string.Empty;

    [JsonPropertyName("subcategorySlug")]
    public string SubcategorySlug { get; set; } = string.Empty;
}

/// <summary>
/// Response wrapper for paginated product list from Catalog Service
/// Matches: ProductListResponse(List[ProductListDto] Products, int Total, bool HasMore, string? NextCursor)
/// </summary>
public class CatalogProductListResponse
{
    [JsonPropertyName("products")]
    public List<CatalogProductDto> Products { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
}
