using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Clients.DTOs;

namespace Gearify.SearchService.Application.Mappers;

/// <summary>
/// Maps Catalog Service product DTOs to Search Service documents.
/// Note: Uses ProductListDto format which is a lightweight version.
/// Some fields (tags, isDeal, etc.) are not available in this format.
/// </summary>
public static class CatalogProductMapper
{
    public static ProductSearchDocument ToSearchDocument(CatalogProductDto product)
    {
        return new ProductSearchDocument
        {
            Id = product.Id,
            TenantId = product.TenantId,
            Name = product.Name,
            Brand = product.Brand,
            BrandSlug = product.BrandSlug,
            Department = product.Department,
            DepartmentSlug = product.DepartmentSlug,
            Category = product.Category,
            CategorySlug = product.CategorySlug,
            Subcategory = product.Subcategory,
            SubcategorySlug = product.SubcategorySlug,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            DiscountPercentage = product.DiscountPercentage,
            Currency = product.Currency,
            ThumbnailUrl = product.ThumbnailUrl,
            RatingAverage = product.RatingAverage,
            RatingCount = product.RatingCount,
            // Fields not available in ProductListDto - use defaults
            Sku = product.Id, // Use ID as SKU fallback
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static IEnumerable<ProductSearchDocument> ToSearchDocuments(IEnumerable<CatalogProductDto> products)
    {
        return products.Select(ToSearchDocument);
    }
}
