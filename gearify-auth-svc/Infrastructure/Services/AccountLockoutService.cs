using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Entities;
using Microsoft.Extensions.Options;
using Gearify.AuthService.Application.Models;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of account lockout management
/// </summary>
public class AccountLockoutService : IAccountLockoutService
{
    private readonly AccountLockoutSettings _lockoutSettings;

    public AccountLockoutService(IOptions<SecurityConfiguration> securityConfig)
    {
        _lockoutSettings = securityConfig.Value.AccountLockout;
    }

    public bool IsLockedOut(User user)
    {
        if (!user.LockoutEnabled || !_lockoutSettings.EnableLockout)
        {
            return false;
        }

        if (user.LockoutEnd == null)
        {
            return false;
        }

        // Check if lockout period has expired
        if (user.LockoutEnd.Value <= DateTime.UtcNow)
        {
            // Lockout expired, but we don't auto-reset here
            // The login handler should call ResetFailedLoginAttempts
            return false;
        }

        return true;
    }

    public bool RecordFailedLoginAttempt(User user)
    {
        if (!user.LockoutEnabled || !_lockoutSettings.EnableLockout)
        {
            return false;
        }

        user.FailedLoginAttempts++;

        // Check if we've reached the lockout threshold
        if (user.FailedLoginAttempts >= _lockoutSettings.MaxFailedAttempts)
        {
            user.LockoutEnd = DateTime.UtcNow.AddMinutes(_lockoutSettings.LockoutDurationMinutes);
            return true; // Account is now locked
        }

        return false; // Not locked yet
    }

    public void ResetFailedLoginAttempts(User user)
    {
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
    }

    public TimeSpan? GetRemainingLockoutTime(User user)
    {
        if (!IsLockedOut(user) || user.LockoutEnd == null)
        {
            return null;
        }

        var remaining = user.LockoutEnd.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public void UnlockAccount(User user)
    {
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
    }
}
