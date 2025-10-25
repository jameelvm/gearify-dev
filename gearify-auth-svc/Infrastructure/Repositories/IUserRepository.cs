using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// Repository interface for user data access
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by their unique identifier
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <returns>The user if found, null otherwise</returns>
    Task<User?> GetByIdAsync(string userId, string tenantId);

    /// <summary>
    /// Gets a user by their email address
    /// </summary>
    /// <param name="email">The email address</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <returns>The user if found, null otherwise</returns>
    Task<User?> GetByEmailAsync(string email, string tenantId);

    /// <summary>
    /// Gets a user by their refresh token
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <returns>The user if found, null otherwise</returns>
    Task<User?> GetByRefreshTokenAsync(string refreshToken, string tenantId);

    /// <summary>
    /// Creates a new user
    /// </summary>
    /// <param name="user">The user to create</param>
    Task CreateAsync(User user);

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="user">The user to update</param>
    Task UpdateAsync(User user);

    /// <summary>
    /// Deletes a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="tenantId">The tenant ID</param>
    Task DeleteAsync(string userId, string tenantId);
}
