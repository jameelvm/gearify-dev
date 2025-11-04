namespace Gearify.AuthService.Domain.Entities;

/// <summary>
/// Represents an active user session
/// </summary>
public class UserSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID this session belongs to
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for this session
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Device information (User-Agent)
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the session
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Geographic location (optional)
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// When the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this session was accessed
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the session is still active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
