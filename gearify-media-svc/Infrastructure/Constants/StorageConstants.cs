namespace Gearify.MediaService.Infrastructure.Constants;

/// <summary>
/// Constants for S3 storage configuration
/// </summary>
public static class StorageConstants
{
    public const string BucketName = "gearify-product-images";
    public const string Region = "us-east-1";

    // Image size configurations (width x height)
    public static class ImageSizes
    {
        public const int ThumbnailSize = 150;
        public const int MediumSize = 600;
        public const int LargeSize = 1200;
    }

    // Image quality settings (0-100)
    public static class ImageQuality
    {
        public const int Thumbnail = 75;
        public const int Medium = 85;
        public const int Large = 90;
        public const int Original = 100;
    }

    // Allowed file types
    public static readonly string[] AllowedContentTypes = new[]
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    // Max file size (10 MB)
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // S3 path template
    public const string S3PathTemplate = "tenants/{0}/{1}/{2}/{3}/{4}"; // tenantId/entityType/entityId/size/filename
}
