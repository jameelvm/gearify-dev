namespace Gearify.CatalogService.Infrastructure.Configuration;

/// <summary>
/// Product upload configuration
/// </summary>
public class ProductImageUploadSettings
{
    /// <summary>
    /// Maximum file size in bytes (default: 10 MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed image content types
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };
}

/// <summary>
/// Messaging configuration for SQS
/// </summary>
public class MessagingSettings
{
    public SqsSettings SQS { get; set; } = new();
}

/// <summary>
/// SQS queue URL configuration
/// </summary>
public class SqsSettings
{
    /// <summary>
    /// Queue URL for receiving product thumbnail update events
    /// </summary>
    public string ProductThumbnailUpdateQueueUrl { get; set; } = string.Empty;
}

/// <summary>
/// Catalog data storage configuration for DynamoDB tables
/// </summary>
public class CatalogDataSettings
{
    /// <summary>
    /// DynamoDB table name for catalog data
    /// </summary>
    public string CatalogTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for product data
    /// </summary>
    public string ProductsTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for brand data
    /// </summary>
    public string BrandsTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for price range data
    /// </summary>
    public string PriceRangesTableName { get; set; } = string.Empty;
}
