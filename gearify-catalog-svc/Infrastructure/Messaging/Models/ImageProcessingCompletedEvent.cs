namespace Gearify.CatalogService.Infrastructure.Messaging.Models;

/// <summary>
/// Event received from Media Service when image processing completes
/// Mirror of Gearify.MediaService.Application.Events.ImageProcessingCompletedEvent
/// </summary>
public record ImageProcessingCompletedEvent(
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
