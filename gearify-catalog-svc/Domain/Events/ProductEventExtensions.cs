using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Domain.Events;

/// <summary>
/// Extension methods for creating domain events from Product entity.
/// </summary>
public static class ProductEventExtensions
{
    public static ProductCreatedEvent ToCreatedEvent(this Product product) => new(
        ProductId: product.Id,
        TenantId: product.TenantId,
        Sku: product.Sku,
        Name: product.Name,
        Description: product.Description,
        Brand: product.Brand,
        BrandSlug: product.BrandSlug,
        Department: product.Department,
        DepartmentSlug: product.DepartmentSlug,
        Category: product.Category,
        CategorySlug: product.CategorySlug,
        Subcategory: product.Subcategory,
        SubcategorySlug: product.SubcategorySlug,
        Price: product.Price,
        CompareAtPrice: product.CompareAtPrice,
        DiscountPercentage: product.DiscountPercentage,
        Currency: product.Currency,
        RatingAverage: product.RatingAverage,
        RatingCount: product.RatingCount,
        ThumbnailUrl: product.ThumbnailUrl,
        ImageUrls: product.ImageUrls,
        Tags: product.Tags,
        IsActive: product.IsActive,
        IsDeal: product.IsDeal,
        IsClearance: product.IsClearance,
        IsNewArrival: product.IsNewArrival,
        IsBestSeller: product.IsBestSeller,
        IsFeatured: product.IsFeatured,
        CreatedAt: product.CreatedAt,
        UpdatedAt: product.UpdatedAt,
        OccurredAt: DateTime.UtcNow);

    public static ProductUpdatedEvent ToUpdatedEvent(this Product product) => new(
        ProductId: product.Id,
        TenantId: product.TenantId,
        Sku: product.Sku,
        Name: product.Name,
        Description: product.Description,
        Brand: product.Brand,
        BrandSlug: product.BrandSlug,
        Department: product.Department,
        DepartmentSlug: product.DepartmentSlug,
        Category: product.Category,
        CategorySlug: product.CategorySlug,
        Subcategory: product.Subcategory,
        SubcategorySlug: product.SubcategorySlug,
        Price: product.Price,
        CompareAtPrice: product.CompareAtPrice,
        DiscountPercentage: product.DiscountPercentage,
        Currency: product.Currency,
        RatingAverage: product.RatingAverage,
        RatingCount: product.RatingCount,
        ThumbnailUrl: product.ThumbnailUrl,
        ImageUrls: product.ImageUrls,
        Tags: product.Tags,
        IsActive: product.IsActive,
        IsDeal: product.IsDeal,
        IsClearance: product.IsClearance,
        IsNewArrival: product.IsNewArrival,
        IsBestSeller: product.IsBestSeller,
        IsFeatured: product.IsFeatured,
        CreatedAt: product.CreatedAt,
        UpdatedAt: product.UpdatedAt,
        OccurredAt: DateTime.UtcNow);
}
