namespace Gearify.AuthService.Domain.Entities;

/// <summary>
/// Represents a saved delivery address for a user
/// </summary>
public class Address
{
    /// <summary>
    /// Unique identifier for the address
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The user who owns this address
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Tenant identifier for multi-tenancy support
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Label for the address (e.g., "Home", "Work", "Mom's House")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Recipient's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Recipient's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Phone number for delivery contact
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Street address line 1
    /// </summary>
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// Street address line 2 (apartment, suite, etc.)
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// State/Province
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// ZIP/Postal code
    /// </summary>
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>
    /// Country code (e.g., "US", "CA", "GB")
    /// </summary>
    public string Country { get; set; } = "US";

    /// <summary>
    /// Whether this is the default delivery address
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Timestamp when the address was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the address was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
