using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for validating passwords against security policies
/// </summary>
public interface IPasswordPolicyService
{
    /// <summary>
    /// Validates a password against the configured password policy
    /// </summary>
    /// <param name="password">The password to validate</param>
    /// <returns>Validation result with any errors</returns>
    PasswordValidationResult ValidatePassword(string password);

    /// <summary>
    /// Checks if a password matches any in the user's password history
    /// </summary>
    /// <param name="password">The password to check</param>
    /// <param name="user">The user whose history to check</param>
    /// <returns>True if password is in history, false otherwise</returns>
    bool IsPasswordInHistory(string password, User user);

    /// <summary>
    /// Adds a password hash to the user's password history
    /// </summary>
    /// <param name="passwordHash">The hashed password to add</param>
    /// <param name="user">The user to update</param>
    void AddToPasswordHistory(string passwordHash, User user);

    /// <summary>
    /// Hashes a password using BCrypt
    /// </summary>
    /// <param name="password">The plain text password</param>
    /// <returns>BCrypt hashed password</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    /// <param name="password">The plain text password</param>
    /// <param name="hash">The hash to verify against</param>
    /// <returns>True if password matches hash</returns>
    bool VerifyPassword(string password, string hash);
}
