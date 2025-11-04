namespace Gearify.AuthService.Application.Models;

/// <summary>
/// Session information for display
/// </summary>
public record SessionInfo
{
    public string SessionId { get; init; } = string.Empty;
    public string DeviceInfo { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string? Location { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastAccessedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsCurrent { get; init; }
}
