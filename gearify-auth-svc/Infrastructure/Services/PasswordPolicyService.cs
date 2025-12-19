using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of password policy validation and management
/// </summary>
public class PasswordPolicyService : IPasswordPolicyService
{
    private readonly PasswordPolicySettings _policySettings;

    public PasswordPolicyService(IOptionsSnapshot<SecurityConfiguration> securityConfig)
    {
        _policySettings = securityConfig.Value.PasswordPolicy;
    }

    public PasswordValidationResult ValidatePassword(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordValidationResult.Failure("Password is required.");
        }

        // Check minimum length
        if (password.Length < _policySettings.MinimumLength)
        {
            errors.Add($"Password must be at least {_policySettings.MinimumLength} characters long.");
        }

        // Check uppercase requirement
        if (_policySettings.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter.");
        }

        // Check lowercase requirement
        if (_policySettings.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter.");
        }

        // Check digit requirement
        if (_policySettings.RequireDigit && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number.");
        }

        // Check special character requirement
        if (_policySettings.RequireSpecialChar && password.All(char.IsLetterOrDigit))
        {
            errors.Add("Password must contain at least one special character.");
        }

        return errors.Any()
            ? PasswordValidationResult.Failure(errors.ToArray())
            : PasswordValidationResult.Success();
    }

    public bool IsPasswordInHistory(string password, User user)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHistory))
        {
            return false;
        }

        var historyHashes = user.PasswordHistory.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return historyHashes.Any(hash => VerifyPassword(password, hash));
    }

    public void AddToPasswordHistory(string passwordHash, User user)
    {
        var historyList = string.IsNullOrWhiteSpace(user.PasswordHistory)
            ? new List<string>()
            : user.PasswordHistory.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Add new hash at the beginning
        historyList.Insert(0, passwordHash);

        // Keep only the configured number of historical passwords
        if (historyList.Count > _policySettings.PasswordHistoryCount)
        {
            historyList = historyList.Take(_policySettings.PasswordHistoryCount).ToList();
        }

        user.PasswordHistory = string.Join(',', historyList);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}
