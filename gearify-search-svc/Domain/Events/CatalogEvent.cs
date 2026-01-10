using System.Text.Json.Serialization;

namespace Gearify.SearchService.Domain.Events;

/// <summary>
/// Base class for all catalog events received from Catalog Service via SNS/SQS
/// </summary>
public class CatalogEvent
{
    /// <summary>
    /// Unique identifier for this event
    /// </summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Type of event: ProductCreated, ProductUpdated, ProductDeleted
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// When the event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Event payload containing product data
    /// </summary>
    [JsonPropertyName("payload")]
    public ProductPayload? Payload { get; set; }
}

/// <summary>
/// Product data payload included in catalog events
/// </summary>
public class ProductPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = string.Empty;

    [JsonPropertyName("brandSlug")]
    public string BrandSlug { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string? Department { get; set; }

    [JsonPropertyName("departmentSlug")]
    public string? DepartmentSlug { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("categorySlug")]
    public string? CategorySlug { get; set; }

    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; set; }

    [JsonPropertyName("subcategorySlug")]
    public string? SubcategorySlug { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("compareAtPrice")]
    public decimal? CompareAtPrice { get; set; }

    [JsonPropertyName("discountPercentage")]
    public decimal? DiscountPercentage { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("ratingAverage")]
    public decimal? RatingAverage { get; set; }

    [JsonPropertyName("ratingCount")]
    public int? RatingCount { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("imageUrls")]
    public List<string> ImageUrls { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("isDeal")]
    public bool IsDeal { get; set; }

    [JsonPropertyName("isClearance")]
    public bool IsClearance { get; set; }

    [JsonPropertyName("isNewArrival")]
    public bool IsNewArrival { get; set; }

    [JsonPropertyName("isBestSeller")]
    public bool IsBestSeller { get; set; }

    [JsonPropertyName("isFeatured")]
    public bool IsFeatured { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
