namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for sending SMS messages
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS message
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number (E.164 format)</param>
    /// <param name="message">Message content</param>
    /// <returns>True if sent successfully</returns>
    Task<bool> SendSmsAsync(string phoneNumber, string message);

    /// <summary>
    /// Sends an OTP code via SMS
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number</param>
    /// <param name="code">OTP code</param>
    /// <param name="expiryMinutes">Code expiry in minutes</param>
    /// <returns>True if sent successfully</returns>
    Task<bool> SendOtpAsync(string phoneNumber, string code, int expiryMinutes);
}
