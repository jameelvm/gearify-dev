using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Interface for JWT token operations
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Generates a JWT access token for a user
    /// </summary>
    /// <param name="user">The user to generate a token for</param>
    /// <returns>The JWT access token</returns>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a refresh token
    /// </summary>
    /// <returns>The refresh token</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Validates a JWT token
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateToken(string token);
}
