namespace Gearify.MediaService.Infrastructure.Configuration;

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

    /// <summary>
    /// Image size settings for variant generation
    /// </summary>
    public ImageSizeSettings ImageSizes { get; set; } = new();

    /// <summary>
    /// Image quality settings for variant generation
    /// </summary>
    public ImageQualitySettings ImageQuality { get; set; } = new();
}

/// <summary>
/// Image size configurations (width/height in pixels)
/// </summary>
public class ImageSizeSettings
{
    public int ThumbnailSize { get; set; } = 150;
    public int MediumSize { get; set; } = 600;
    public int LargeSize { get; set; } = 1200;
}

/// <summary>
/// Image quality settings (0-100)
/// </summary>
public class ImageQualitySettings
{
    public int Thumbnail { get; set; } = 75;
    public int Medium { get; set; } = 85;
    public int Large { get; set; } = 90;
    public int Original { get; set; } = 100;
}