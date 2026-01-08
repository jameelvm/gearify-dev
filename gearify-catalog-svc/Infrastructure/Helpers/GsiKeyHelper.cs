using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Helpers;

/// <summary>
/// Helper class to compute DynamoDB GSI keys for product sorting
/// </summary>
public static class GsiKeyHelper
{
    /// <summary>
    /// Compute GSI2SK for price sorting
    /// Format: PRICE#{10-digit-zero-padded-cents}#PRODUCT#{productId}
    /// Example: PRICE#0000129999#PRODUCT#prod-201 for $1299.99
    /// </summary>
    public static string ComputePriceSortKey(Product product)
    {
        // Convert price to cents and pad to 10 digits
        var cents = (long)(product.Price * 100);
        var paddedCents = cents.ToString("D10");
        return $"PRICE#{paddedCents}#PRODUCT#{product.Id}";
    }

    /// <summary>
    /// Compute GSI3SK for rating sorting
    /// Format: RATING#{5-digit-zero-padded-hundredths}#PRODUCT#{productId}
    /// Example: RATING#00450#PRODUCT#prod-201 for 4.5 stars
    /// Null ratings get 00000 (will appear first when sorted ascending)
    /// </summary>
    public static string ComputeRatingSortKey(Product product)
    {
        // Convert rating to hundredths (4.5 => 450) and pad to 5 digits
        // Use 0 for null ratings so they appear first
        var hundredths = (long)((product.RatingAverage ?? 0) * 100);
        var paddedRating = hundredths.ToString("D5");
        return $"RATING#{paddedRating}#PRODUCT#{product.Id}";
    }

    /// <summary>
    /// Compute GSI4SK for newest/date sorting
    /// Format: CREATEDAT#{ISO8601-timestamp}#PRODUCT#{productId}
    /// Example: CREATEDAT#2025-01-17T10:00:00.000Z#PRODUCT#prod-201
    /// </summary>
    public static string ComputeCreatedAtSortKey(Product product)
    {
        var timestamp = product.CreatedAt.ToString("O"); // ISO 8601 format
        return $"CREATEDAT#{timestamp}#PRODUCT#{product.Id}";
    }

    /// <summary>
    /// Compute GSI5SK for name sorting (A-Z)
    /// Format: NAME#{lowercase-normalized-name}#PRODUCT#{productId}
    /// Example: NAME#ss ton reserve edition english willow cricket bat#PRODUCT#prod-201
    /// </summary>
    public static string ComputeNameSortKey(Product product)
    {
        // Normalize: lowercase and trim whitespace
        var normalizedName = product.Name.ToLowerInvariant().Trim();
        return $"NAME#{normalizedName}#PRODUCT#{product.Id}";
    }

    /// <summary>
    /// Compute GSI6PK and GSI6SK for featured products (sparse index)
    /// Only products with IsFeatured=true will have these keys set
    /// </summary>
    public static (string? GSI6PK, string? GSI6SK) ComputeFeaturedSortKeys(Product product)
    {
        if (!product.IsFeatured)
        {
            return (null, null);
        }

        var gsi6PK = $"TENANT#{product.TenantId}#FEATURED";
        var gsi6SK = ComputeCreatedAtSortKey(product); // Sort featured by newest first

        return (gsi6PK, gsi6SK);
    }
}
