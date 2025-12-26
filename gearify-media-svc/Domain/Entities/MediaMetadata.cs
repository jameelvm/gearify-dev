using Gearify.MediaService.Domain.Enums;

namespace Gearify.MediaService.Domain.Entities;

/// <summary>
/// Metadata for media files stored in S3
/// Supports multiple entity types (products, brands, categories, etc.)
/// </summary>
public class MediaMetadata
{
    // DynamoDB Keys
    public string PK { get; set; } = string.Empty;      // TENANT#{tenantId}
    public string SK { get; set; } = string.Empty;      // MEDIA#{mediaId}

    // GSI1 for querying by entity
    public string GSI1PK { get; set; } = string.Empty;  // TENANT#{tenantId}#{entityType}#{entityId}
    public string GSI1SK { get; set; } = string.Empty;  // MEDIA#{mediaId}

    // Core attributes
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    // Generic entity reference
    public string EntityType { get; set; } = Enums.EntityType.Product.ToString();
    public string EntityId { get; set; } = string.Empty;

    // File metadata
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }

    // Image dimensions (original)
    public int? Width { get; set; }
    public int? Height { get; set; }

    // S3 storage keys
    public string OriginalKey { get; set; } = string.Empty;
    public string ThumbnailKey { get; set; } = string.Empty;
    public string MediumKey { get; set; } = string.Empty;
    public string LargeKey { get; set; } = string.Empty;

    // Display order (for galleries)
    public int DisplayOrder { get; set; }

    // Alt text for accessibility
    public string? AltText { get; set; }

    // Metadata
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
