namespace Gearify.MediaService.Application.Events;

/// <summary>
/// Event published when image processing (variant generation) is completed
/// Used to notify other services (like Catalog Service) to update their records
/// </summary>
public record ImageProcessingCompletedEvent(
    string MediaId,
    string EntityType,    // "Product", "Brand", "Category", etc.
    string EntityId,      // e.g., "prod-004"
    string TenantId,
    string ThumbnailUrl,
    string MediumUrl,
    string LargeUrl,
    string OriginalUrl,
    int DisplayOrder,
    string? AltText
)
{
    /// <summary>
    /// SNS Topic name for this event
    /// </summary>
    public const string TopicName = "gearify-image-processing-completed";
}
