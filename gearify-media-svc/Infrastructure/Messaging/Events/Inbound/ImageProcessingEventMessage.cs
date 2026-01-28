namespace Gearify.MediaService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Image processing event message received via SQS (wrapped in EventEnvelope).
/// </summary>
public record ImageProcessingEventMessage
{
    /// <summary>
    /// Event type extracted from the EventEnvelope
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    public string MediaId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string OriginalKey { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime UploadedAt { get; init; }

    // Alias for SNS event compatibility (MediaUploadedEvent uses OccurredAt)
    public DateTime OccurredAt { init => UploadedAt = value; }
}
