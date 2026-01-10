namespace Gearify.SearchService.Application.DTOs;

public class SearchProductsResponse
{
    public List<ProductSearchItem> Items { get; set; } = new();
    public SearchFacets Facets { get; set; } = new();
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ProductSearchItem
{
    public string Id { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? DepartmentSlug { get; set; }
    public string? Category { get; set; }
    public string? CategorySlug { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }
    public bool IsDeal { get; set; }
    public bool IsClearance { get; set; }
    public bool IsNewArrival { get; set; }
    public bool IsBestSeller { get; set; }
}

public class SearchFacets
{
    public List<FacetItem> Brands { get; set; } = new();
    public List<FacetItem> Categories { get; set; } = new();
    public List<FacetItem> Departments { get; set; } = new();
    public List<FacetItem> PriceRanges { get; set; } = new();
    public List<FacetItem> Ratings { get; set; } = new();
}

public class FacetItem
{
    public string Key { get; set; } = string.Empty;
    public long Count { get; set; }
}
