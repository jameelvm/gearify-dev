namespace Gearify.MediaService.Application.Events;

/// <summary>
/// Event published when original media file is uploaded
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
    DateTime UploadedAt)
{
    /// <summary>
    /// SNS Topic name for this event
    /// </summary>
    public const string TopicName = "gearify-media-upload-events";
}
