namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Represents a price range filter configuration for a tenant
/// </summary>
public class PriceRange
{
    /// <summary>
    /// Unique identifier for the price range
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Tenant identifier
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

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
    /// Display order for sorting (lower numbers appear first)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Optional category filter - if set, this range only applies to specific category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether this price range is active and should be displayed
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this price range was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this price range was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User who created this price range
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// User who last updated this price range
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;
}
