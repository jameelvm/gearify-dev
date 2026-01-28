namespace Gearify.CatalogService.Infrastructure.Messaging.Events.Inbound;

/// <summary>
/// Event message received from Media Service when image processing completes (wrapped in EventEnvelope).
/// </summary>
public record ImageProcessingCompletedEventMessage
{
    /// <summary>
    /// Event type extracted from the EventEnvelope
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    public string MediaId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string TenantId { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string MediumUrl { get; init; } = string.Empty;
    public string LargeUrl { get; init; } = string.Empty;
    public string OriginalUrl { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public string? AltText { get; init; }
}
