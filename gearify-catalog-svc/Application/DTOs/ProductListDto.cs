using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Application.DTOs;

/// <summary>
/// Lightweight DTO for product list views
/// Contains only essential fields for displaying products in grids/lists
/// </summary>
public class ProductListDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;

    // Pricing
    public decimal Price { get; set; }
    public decimal CompareAtPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string? OfferBadge { get; set; }
    public string Currency { get; set; } = "USD";

    // Primary image for list view (updated by Media Service background job)
    public string? ThumbnailUrl { get; set; }

    // Ratings
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }

    // Catalog hierarchy (for filtering/navigation)
    public string Department { get; set; } = string.Empty;
    public string DepartmentSlug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string SubcategorySlug { get; set; } = string.Empty;

    /// <summary>
    /// Map Product entity to ProductListDto
    /// </summary>
    public static ProductListDto FromProduct(Product product)
    {
        return new ProductListDto
        {
            Id = product.Id,
            Name = product.Name,
            Brand = product.Brand,
            BrandSlug = product.BrandSlug,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            DiscountPercentage = product.DiscountPercentage,
            OfferBadge = product.OfferBadge,
            Currency = product.Currency,
            ThumbnailUrl = product.ThumbnailUrl,
            RatingAverage = product.RatingAverage,
            RatingCount = product.RatingCount,
            Department = product.Department,
            DepartmentSlug = product.DepartmentSlug,
            Category = product.Category,
            CategorySlug = product.CategorySlug,
            Subcategory = product.Subcategory,
            SubcategorySlug = product.SubcategorySlug
        };
    }
}
