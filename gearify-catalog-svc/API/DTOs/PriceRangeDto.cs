namespace Gearify.CatalogService.API.DTOs;

/// <summary>
/// DTO for price range filter configuration
/// </summary>
public class PriceRangeDto
{
    /// <summary>
    /// Unique identifier for the price range
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display label for the price range (e.g., "Under $50", "$50-$100")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Minimum price for this range (inclusive)
    /// </summary>
    public decimal MinPrice { get; set; }

    /// <summary>
    /// Maximum price for this range (inclusive, null means no upper limit)
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD", "EUR")
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Optional category filter - if set, this range only applies to specific category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Number of products in this price range (calculated dynamically)
    /// </summary>
    public int ProductCount { get; set; }

    /// <summary>
    /// Value representation for filtering (e.g., "0-50", "500+")
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
