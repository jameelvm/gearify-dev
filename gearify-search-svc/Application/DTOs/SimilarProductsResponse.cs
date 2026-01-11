namespace Gearify.SearchService.Application.DTOs;

public class SimilarProductsResponse
{
    public List<ProductSearchItem> Items { get; set; } = new();
    public string ProductId { get; set; } = string.Empty;
    public string? MatchStrategy { get; set; }
}
