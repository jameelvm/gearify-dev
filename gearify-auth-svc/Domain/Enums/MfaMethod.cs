namespace Gearify.AuthService.Domain.Enums;

/// <summary>
/// Multi-factor authentication methods
/// </summary>
public enum MfaMethod
{
    /// <summary>
    /// No MFA enabled
    /// </summary>
    None = 0,

    /// <summary>
    /// Time-based One-Time Password (Google Authenticator, Authy, etc.)
    /// </summary>
    Totp = 1,

    /// <summary>
    /// Email-based One-Time Password
    /// </summary>
    Email = 2,

    /// <summary>
    /// SMS-based One-Time Password
    /// </summary>
    Sms = 3
}
