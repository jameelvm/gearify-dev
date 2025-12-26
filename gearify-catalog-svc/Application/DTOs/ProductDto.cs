using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Clients.DTOs;

namespace Gearify.CatalogService.Application.DTOs;

/// <summary>
/// Product DTO with enriched image data from Media Service
/// </summary>
public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Catalog hierarchy
    public string Department { get; set; } = string.Empty;
    public string DepartmentSlug { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string SubcategorySlug { get; set; } = string.Empty;

    // Brand
    public string Brand { get; set; } = string.Empty;
    public string BrandSlug { get; set; } = string.Empty;

    // Pricing
    public decimal Price { get; set; }
    public decimal CompareAtPrice { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public string? OfferBadge { get; set; }
    public string Currency { get; set; } = "USD";

    // Images - enriched from Media Service
    public List<ProductImageDto> Images { get; set; } = new();

    // Legacy field for backward compatibility
    public List<string> ImageUrls { get; set; } = new();

    // Tags and attributes
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Attributes { get; set; } = new();

    // Ratings
    public decimal? RatingAverage { get; set; }
    public int? RatingCount { get; set; }

    // Metadata
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Create ProductDto from Product entity and media metadata
    /// </summary>
    public static ProductDto FromProduct(Product product, List<MediaMetadataDto>? images = null)
    {
        var dto = new ProductDto
        {
            Id = product.Id,
            TenantId = product.TenantId,
            Sku = product.Sku,
            Name = product.Name,
            Description = product.Description,
            Department = product.Department,
            DepartmentSlug = product.DepartmentSlug,
            Category = product.Category,
            CategorySlug = product.CategorySlug,
            Subcategory = product.Subcategory,
            SubcategorySlug = product.SubcategorySlug,
            Brand = product.Brand,
            BrandSlug = product.BrandSlug,
            Price = product.Price,
            CompareAtPrice = product.CompareAtPrice,
            DiscountPercentage = product.DiscountPercentage,
            OfferBadge = product.OfferBadge,
            Currency = product.Currency,
            Tags = product.Tags,
            Attributes = product.Attributes,
            RatingAverage = product.RatingAverage,
            RatingCount = product.RatingCount,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            CreatedBy = product.CreatedBy,
            UpdatedBy = product.UpdatedBy
        };

        // Populate images from Media Service
        if (images != null && images.Any())
        {
            dto.Images = images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new ProductImageDto
                {
                    MediaId = i.Id,
                    OriginalUrl = i.Urls.GetValueOrDefault("original") ?? "",
                    ThumbnailUrl = i.Urls.GetValueOrDefault("thumbnail") ?? "",
                    MediumUrl = i.Urls.GetValueOrDefault("medium") ?? "",
                    LargeUrl = i.Urls.GetValueOrDefault("large") ?? "",
                    AltText = i.AltText,
                    DisplayOrder = i.DisplayOrder
                })
                .ToList();

            // Populate legacy ImageUrls field with medium size URLs for backward compatibility
            dto.ImageUrls = dto.Images
                .Select(i => i.MediumUrl)
                .Where(url => !string.IsNullOrEmpty(url))
                .ToList();
        }
        else
        {
            // Fallback to product's ImageUrls if no images from Media Service
            dto.ImageUrls = product.ImageUrls;
        }

        return dto;
    }
}

/// <summary>
/// Product image DTO with all size variants
/// </summary>
public class ProductImageDto
{
    public string MediaId { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string MediumUrl { get; set; } = string.Empty;
    public string LargeUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int DisplayOrder { get; set; }
}
