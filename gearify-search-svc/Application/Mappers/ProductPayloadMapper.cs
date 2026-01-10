using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Domain.Events;

namespace Gearify.SearchService.Application.Mappers;

/// <summary>
/// Maps ProductPayload from catalog events to ProductSearchDocument for indexing
/// </summary>
public static class ProductPayloadMapper
{
    /// <summary>
    /// Convert a ProductPayload from catalog event to ProductSearchDocument for OpenSearch
    /// </summary>
    public static ProductSearchDocument ToSearchDocument(ProductPayload payload)
    {
        return new ProductSearchDocument
        {
            Id = payload.Id,
            TenantId = payload.TenantId,
            Sku = payload.Sku,
            Name = payload.Name,
            Description = payload.Description,
            Brand = payload.Brand,
            BrandSlug = payload.BrandSlug,
            Department = payload.Department,
            DepartmentSlug = payload.DepartmentSlug,
            Category = payload.Category,
            CategorySlug = payload.CategorySlug,
            Subcategory = payload.Subcategory,
            SubcategorySlug = payload.SubcategorySlug,
            Price = payload.Price,
            CompareAtPrice = payload.CompareAtPrice,
            DiscountPercentage = payload.DiscountPercentage,
            Currency = payload.Currency,
            RatingAverage = payload.RatingAverage,
            RatingCount = payload.RatingCount,
            ThumbnailUrl = payload.ThumbnailUrl,
            ImageUrls = payload.ImageUrls,
            Tags = payload.Tags,
            IsActive = payload.IsActive,
            IsDeal = payload.IsDeal,
            IsClearance = payload.IsClearance,
            IsNewArrival = payload.IsNewArrival,
            IsBestSeller = payload.IsBestSeller,
            IsFeatured = payload.IsFeatured,
            CreatedAt = payload.CreatedAt,
            UpdatedAt = payload.UpdatedAt
        };
    }
}
