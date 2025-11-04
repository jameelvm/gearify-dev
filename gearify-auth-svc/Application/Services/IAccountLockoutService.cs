using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for managing account lockout functionality
/// </summary>
public interface IAccountLockoutService
{
    /// <summary>
    /// Checks if a user account is currently locked out
    /// </summary>
    /// <param name="user">The user to check</param>
    /// <returns>True if account is locked, false otherwise</returns>
    bool IsLockedOut(User user);

    /// <summary>
    /// Records a failed login attempt and potentially locks the account
    /// </summary>
    /// <param name="user">The user who failed login</param>
    /// <returns>True if account was locked due to this attempt</returns>
    bool RecordFailedLoginAttempt(User user);

    /// <summary>
    /// Resets failed login attempts after successful login
    /// </summary>
    /// <param name="user">The user who successfully logged in</param>
    void ResetFailedLoginAttempts(User user);

    /// <summary>
    /// Gets the remaining time for account lockout
    /// </summary>
    /// <param name="user">The locked user</param>
    /// <returns>TimeSpan remaining, or null if not locked</returns>
    TimeSpan? GetRemainingLockoutTime(User user);

    /// <summary>
    /// Manually unlocks a user account (admin function)
    /// </summary>
    /// <param name="user">The user to unlock</param>
    void UnlockAccount(User user);
}
