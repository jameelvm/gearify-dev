namespace Gearify.CatalogService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event message received from Media Service when image processing completes.
/// </summary>
public record ImageProcessingCompletedEventMessage(
    string MediaId,
    string EntityType,
    string EntityId,
    string TenantId,
    string ThumbnailUrl,
    string MediumUrl,
    string LargeUrl,
    string OriginalUrl,
    int DisplayOrder,
    string? AltText
);
