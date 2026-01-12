using Gearify.SharedKernel.Events;

namespace Gearify.MediaService.Domain.Events;

/// <summary>
/// Domain event raised when image processing (variant generation) is completed.
/// Used to notify other services (like Catalog Service) to update their records.
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
    string? AltText,
    DateTime OccurredAt) : IDomainEvent;
