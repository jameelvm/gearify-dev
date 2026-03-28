namespace Gearify.SharedKernel.AI.Events;

public record UserInteractionEvent
{
    public string UserId { get; init; } = "anonymous";
    public string? ProductId { get; init; }
    public string TenantId { get; init; } = "default";
    public string EventType { get; init; } = string.Empty;
    public decimal? EventValue { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public static class InteractionEventTypes
{
    public const string View = "View";
    public const string AddToCart = "AddToCart";
    public const string Purchase = "Purchase";
    public const string Search = "Search";
}
