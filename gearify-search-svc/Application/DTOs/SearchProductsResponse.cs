namespace Gearify.SearchService.Application.DTOs;

public class SearchProductsResponse
{
    public List<ProductSearchResult> Results { get; set; } = new();
    public SearchFacets Facets { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class ProductSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? RatingAverage { get; set; }
    public string? ThumbnailUrl { get; set; }
    public double Score { get; set; }
}

public class SearchFacets
{
    public List<FacetItem> Brands { get; set; } = new();
    public List<FacetItem> Categories { get; set; } = new();
    public List<FacetItem> PriceRanges { get; set; } = new();
}

public class FacetItem
{
    public string Key { get; set; } = string.Empty;
    public long Count { get; set; }
}
