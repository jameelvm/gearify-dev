namespace Gearify.MediaService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Image processing event message received via SQS.
/// </summary>
public class ImageProcessingEventMessage
{
    public string MediaId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OriginalKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime UploadedAt { get; set; }

    // Alias for SNS event compatibility (MediaUploadedEvent uses OccurredAt)
    public DateTime OccurredAt { set => UploadedAt = value; }
}
