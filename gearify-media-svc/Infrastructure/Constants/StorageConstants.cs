namespace Gearify.MediaService.Infrastructure.Constants;

/// <summary>
/// Constants for S3 storage paths and templates
/// Note: Upload limits, image sizes, and quality settings have been moved to ProductImageUploadConfiguration
/// </summary>
public static class StorageConstants
{
    // S3 path template: tenantId/entityType/entityId/size/filename
    public const string S3PathTemplate = "tenants/{0}/{1}/{2}/{3}/{4}";
}
