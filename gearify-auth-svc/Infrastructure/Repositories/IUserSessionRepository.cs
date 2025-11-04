using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// Repository for user session operations
/// </summary>
public interface IUserSessionRepository
{
    /// <summary>
    /// Creates a new session
    /// </summary>
    Task CreateAsync(UserSession session);

    /// <summary>
    /// Gets a session by ID
    /// </summary>
    Task<UserSession?> GetByIdAsync(string sessionId, string userId);

    /// <summary>
    /// Gets a session by refresh token
    /// </summary>
    Task<UserSession?> GetByRefreshTokenAsync(string userId, string refreshToken);

    /// <summary>
    /// Gets all sessions for a user
    /// </summary>
    Task<List<UserSession>> GetUserSessionsAsync(string userId);

    /// <summary>
    /// Gets all active sessions for a user
    /// </summary>
    Task<List<UserSession>> GetActiveSessionsAsync(string userId);

    /// <summary>
    /// Updates a session
    /// </summary>
    Task UpdateAsync(UserSession session);

    /// <summary>
    /// Deletes a session
    /// </summary>
    Task DeleteAsync(string sessionId, string userId);

    /// <summary>
    /// Deletes all sessions for a user
    /// </summary>
    Task DeleteAllUserSessionsAsync(string userId, string? exceptSessionId = null);

    /// <summary>
    /// Deletes expired sessions for a user
    /// </summary>
    Task<int> DeleteExpiredSessionsAsync(string userId);
}
