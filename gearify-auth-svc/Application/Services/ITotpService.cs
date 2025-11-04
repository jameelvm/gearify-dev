namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for Time-based One-Time Password (TOTP) operations
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Generates a new TOTP secret for a user
    /// </summary>
    /// <returns>Base32-encoded secret</returns>
    string GenerateSecret();

    /// <summary>
    /// Generates a QR code for TOTP setup
    /// </summary>
    /// <param name="email">User's email</param>
    /// <param name="secret">TOTP secret</param>
    /// <param name="issuer">App issuer name</param>
    /// <returns>Base64-encoded PNG image</returns>
    string GenerateQrCode(string email, string secret, string issuer = "Gearify");

    /// <summary>
    /// Verifies a TOTP code against a secret
    /// </summary>
    /// <param name="secret">TOTP secret</param>
    /// <param name="code">6-digit code to verify</param>
    /// <param name="timeToleranceSeconds">Time tolerance for verification (default 30 seconds)</param>
    /// <returns>True if code is valid</returns>
    bool VerifyCode(string secret, string code, int timeToleranceSeconds = 30);

    /// <summary>
    /// Gets the manual entry key formatted for user display
    /// </summary>
    /// <param name="secret">TOTP secret</param>
    /// <returns>Formatted secret key</returns>
    string FormatSecretForDisplay(string secret);
}
