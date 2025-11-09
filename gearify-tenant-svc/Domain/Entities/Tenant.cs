using System;

namespace Gearify.TenantService.Domain.Entities;

/// <summary>
/// Represents a tenant in the system
/// </summary>
public class Tenant
{
    /// <summary>
    /// Unique identifier for the tenant (slug used in subdomain)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the tenant
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tenant is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the tenant was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the tenant was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Custom domain (optional) e.g., "acme.com"
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// Tenant subscription plan (Free, Pro, Enterprise, etc.)
    /// </summary>
    public string Plan { get; set; } = "Free";

    /// <summary>
    /// Maximum number of users allowed
    /// </summary>
    public int MaxUsers { get; set; } = 10;

    /// <summary>
    /// Contact email for the tenant
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
}
