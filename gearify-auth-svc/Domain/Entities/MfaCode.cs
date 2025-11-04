using Gearify.AuthService.Domain.Enums;

namespace Gearify.AuthService.Domain.Entities;

/// <summary>
/// Represents a multi-factor authentication code
/// </summary>
public class MfaCode
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID this code belongs to
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The verification code (hashed)
    /// </summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>
    /// MFA method used
    /// </summary>
    public MfaMethod Method { get; set; }

    /// <summary>
    /// When the code was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the code expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the code has been used
    /// </summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>
    /// Number of verification attempts
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Purpose of the code (e.g., "login", "setup", "disable")
    /// </summary>
    public string Purpose { get; set; } = "login";
}
