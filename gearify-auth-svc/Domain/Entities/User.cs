namespace Gearify.AuthService.Domain.Entities;

/// <summary>
/// Represents a user in the authentication system
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the tenant identifier for multi-tenancy support
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hashed password (BCrypt)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's phone number
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's role (e.g., Admin, Customer, Manager)
    /// </summary>
    public string Role { get; set; } = "Customer";

    /// <summary>
    /// Gets or sets the timestamp when the user was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the user was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp of the user's last login
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Gets or sets whether the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the user's email has been verified
    /// </summary>
    public bool EmailVerified { get; set; } = false;

    /// <summary>
    /// Gets or sets the current refresh token for the user
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the expiry date/time for the refresh token
    /// </summary>
    public DateTime? RefreshTokenExpiry { get; set; }

    /// <summary>
    /// Gets or sets the email verification token
    /// </summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>
    /// Gets or sets the expiry date/time for the email verification token
    /// </summary>
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    // ==================== Password Reset Fields ====================

    /// <summary>
    /// Gets or sets the password reset token
    /// </summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>
    /// Gets or sets the expiry date/time for the password reset token
    /// </summary>
    public DateTime? PasswordResetTokenExpiry { get; set; }

    /// <summary>
    /// Gets or sets when password was last changed
    /// </summary>
    public DateTime? LastPasswordChangeAt { get; set; }

    // ==================== Account Lockout Fields ====================

    /// <summary>
    /// Gets or sets the number of failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>
    /// Gets or sets when the account lockout ends
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// Gets or sets whether lockout is enabled for this user
    /// </summary>
    public bool LockoutEnabled { get; set; } = true;

    // ==================== Password History ====================

    /// <summary>
    /// Gets or sets password history (last 5 hashed passwords, comma-separated)
    /// </summary>
    public string? PasswordHistory { get; set; }

    // ==================== Session Tracking ====================

    /// <summary>
    /// Gets or sets the count of active sessions
    /// </summary>
    public int ActiveSessionCount { get; set; } = 0;

    // ==================== Default Address ====================

    /// <summary>
    /// Gets or sets the street address line 1
    /// </summary>
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the street address line 2 (apartment, suite, etc.)
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// Gets or sets the city
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the state/province
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the ZIP/postal code
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Gets or sets the country (defaults to USA)
    /// </summary>
    public string? Country { get; set; } = "USA";
}
