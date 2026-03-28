namespace Gearify.CatalogService.Application.DTOs;

public class ProductRecommendation
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public double Score { get; set; }
    public string? RecommendationReason { get; set; }
}

public static class RecommendationSources
{
    public const string Personalize = "personalize";
    public const string Cache = "cache";
    public const string ComplementaryRules = "complementary-rules";
    public const string CategoryFallback = "category-fallback";
    public const string PopularFallback = "popular-fallback";
    public const string Empty = "empty";
}

public record RecommendationResponse
{
    public List<ProductRecommendation> Items { get; set; } = new();
    public string Source { get; set; } = string.Empty;
    public int TotalCount { get; set; }
}
