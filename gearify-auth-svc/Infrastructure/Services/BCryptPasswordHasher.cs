using BCrypt.Net;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// BCrypt implementation of password hasher
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    private const int SaltRounds = 12;

    /// <summary>
    /// Hashes a plaintext password using BCrypt with 12 salt rounds
    /// </summary>
    /// <param name="password">The plaintext password</param>
    /// <returns>The BCrypt hashed password</returns>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, SaltRounds);
    }

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash
    /// </summary>
    /// <param name="password">The plaintext password</param>
    /// <param name="hash">The BCrypt password hash</param>
    /// <returns>True if the password matches the hash, false otherwise</returns>
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
