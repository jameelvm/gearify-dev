namespace Gearify.CatalogService.Infrastructure.Configuration;

/// <summary>
/// Product upload configuration
/// </summary>
public class ProductImageUploadConfiguration
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