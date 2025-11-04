using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for Email/SMS OTP operations
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a random 6-digit OTP code
    /// </summary>
    /// <returns>6-digit numeric code</returns>
    string GenerateCode();

    /// <summary>
    /// Stores an OTP code for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="tenantId">Tenant ID</param>
    /// <param name="code">OTP code</param>
    /// <param name="method">MFA method (Email or SMS)</param>
    /// <param name="purpose">Purpose of the code (Login, Setup, etc.)</param>
    /// <param name="expiryMinutes">Code expiry in minutes</param>
    /// <returns>MfaCode entity</returns>
    Task<MfaCode> StoreCodeAsync(string userId, string tenantId, string code, Domain.Enums.MfaMethod method, string purpose, int expiryMinutes);

    /// <summary>
    /// Verifies an OTP code
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="code">Code to verify</param>
    /// <param name="purpose">Purpose of the code</param>
    /// <returns>True if code is valid and not expired</returns>
    Task<bool> VerifyCodeAsync(string userId, string code, string purpose);

    /// <summary>
    /// Invalidates/marks as used an OTP code
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="code">Code to invalidate</param>
    /// <returns>Task</returns>
    Task InvalidateCodeAsync(string userId, string code);

    /// <summary>
    /// Cleans up expired OTP codes for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of codes deleted</returns>
    Task<int> CleanupExpiredCodesAsync(string userId);
}
