using Gearify.SharedKernel.Events;

namespace Gearify.CatalogService.Domain.Events;

/// <summary>
/// Domain event raised when a product is updated.
/// Contains all product data needed for consumers (e.g., Search Service re-indexing).
/// </summary>
public record ProductUpdatedEvent(
    string ProductId,
    string TenantId,
    string Sku,
    string Name,
    string? Description,
    string Brand,
    string BrandSlug,
    string? Department,
    string? DepartmentSlug,
    string? Category,
    string? CategorySlug,
    string? Subcategory,
    string? SubcategorySlug,
    decimal Price,
    decimal CompareAtPrice,
    decimal? DiscountPercentage,
    string Currency,
    decimal? RatingAverage,
    int? RatingCount,
    string? ThumbnailUrl,
    List<string> ImageUrls,
    List<string> Tags,
    bool IsActive,
    bool IsDeal,
    bool IsClearance,
    bool IsNewArrival,
    bool IsBestSeller,
    bool IsFeatured,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime OccurredAt) : IDomainEvent;
