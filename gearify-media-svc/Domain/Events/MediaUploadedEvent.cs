using Gearify.SharedKernel.Events;

namespace Gearify.MediaService.Domain.Events;

/// <summary>
/// Domain event raised when original media file is uploaded.
/// </summary>
public record MediaUploadedEvent(
    string MediaId,
    string TenantId,
    string EntityType,
    string EntityId,
    string OriginalKey,
    string ContentType,
    int Width,
    int Height,
    DateTime OccurredAt) : IDomainEvent;
