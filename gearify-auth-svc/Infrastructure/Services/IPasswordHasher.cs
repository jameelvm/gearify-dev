namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Interface for password hashing operations
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password
    /// </summary>
    /// <param name="password">The plaintext password</param>
    /// <returns>The hashed password</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a hash
    /// </summary>
    /// <param name="password">The plaintext password</param>
    /// <param name="hash">The password hash</param>
    /// <returns>True if the password matches the hash, false otherwise</returns>
    bool VerifyPassword(string password, string hash);
}
