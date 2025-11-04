using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for managing user sessions
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new session for a user
    /// </summary>
    Task<UserSession> CreateSessionAsync(string userId, string tenantId, string refreshToken, string deviceInfo, string ipAddress, string? location = null);

    /// <summary>
    /// Gets all active sessions for a user
    /// </summary>
    Task<List<SessionInfo>> GetActiveSessionsAsync(string userId);

    /// <summary>
    /// Revokes a specific session
    /// </summary>
    Task<bool> RevokeSessionAsync(string userId, string sessionId);

    /// <summary>
    /// Revokes all sessions for a user (except optionally the current one)
    /// </summary>
    Task<int> RevokeAllSessionsAsync(string userId, string? exceptSessionId = null);

    /// <summary>
    /// Cleans up expired sessions
    /// </summary>
    Task<int> CleanupExpiredSessionsAsync(string userId);

    /// <summary>
    /// Validates a session by refresh token
    /// </summary>
    Task<UserSession?> ValidateSessionAsync(string userId, string refreshToken);

    /// <summary>
    /// Updates session last accessed time
    /// </summary>
    Task UpdateSessionAccessAsync(string sessionId);
}
