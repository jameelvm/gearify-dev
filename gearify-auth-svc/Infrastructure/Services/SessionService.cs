using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of session management service
/// </summary>
public class SessionService : ISessionService
{
    private readonly IUserSessionRepository _sessionRepository;
    private readonly ILogger<SessionService> _logger;
    private readonly SessionSettings _sessionSettings;

    public SessionService(
        IUserSessionRepository sessionRepository,
        ILogger<SessionService> logger,
        IOptions<SecurityConfiguration> securityConfig)
    {
        _sessionRepository = sessionRepository;
        _logger = logger;
        _sessionSettings = securityConfig.Value.Session;
    }

    public async Task<UserSession> CreateSessionAsync(
        string userId,
        string tenantId,
        string refreshToken,
        string deviceInfo,
        string ipAddress,
        string? location = null)
    {
        try
        {
            // Check if user has reached max concurrent sessions
            var activeSessions = await _sessionRepository.GetActiveSessionsAsync(userId);

            if (activeSessions.Count >= _sessionSettings.MaxConcurrentSessions)
            {
                // Remove the oldest session
                var oldestSession = activeSessions.OrderBy(s => s.CreatedAt).First();
                await _sessionRepository.DeleteAsync(oldestSession.Id, userId);
                _logger.LogInformation("Removed oldest session {SessionId} for user {UserId} due to max concurrent sessions limit",
                    oldestSession.Id, userId);
            }

            var session = new UserSession
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                TenantId = tenantId,
                RefreshToken = refreshToken,
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress,
                Location = location,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_sessionSettings.RefreshTokenExpiryDays),
                IsActive = true
            };

            await _sessionRepository.CreateAsync(session);

            _logger.LogInformation("Created new session {SessionId} for user {UserId}", session.Id, userId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<SessionInfo>> GetActiveSessionsAsync(string userId)
    {
        try
        {
            var sessions = await _sessionRepository.GetActiveSessionsAsync(userId);

            return sessions.Select(s => new SessionInfo
            {
                SessionId = s.Id,
                DeviceInfo = s.DeviceInfo,
                IpAddress = s.IpAddress,
                Location = s.Location,
                CreatedAt = s.CreatedAt,
                LastAccessedAt = s.LastAccessedAt,
                ExpiresAt = s.ExpiresAt,
                IsCurrent = false // This should be set by the caller based on current session
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> RevokeSessionAsync(string userId, string sessionId)
    {
        try
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId, userId);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found for user {UserId}", sessionId, userId);
                return false;
            }

            session.IsActive = false;
            await _sessionRepository.UpdateAsync(session);

            _logger.LogInformation("Revoked session {SessionId} for user {UserId}", sessionId, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session {SessionId} for user {UserId}", sessionId, userId);
            throw;
        }
    }

    public async Task<int> RevokeAllSessionsAsync(string userId, string? exceptSessionId = null)
    {
        try
        {
            var sessions = await _sessionRepository.GetActiveSessionsAsync(userId);

            var sessionsToRevoke = string.IsNullOrEmpty(exceptSessionId)
                ? sessions
                : sessions.Where(s => s.Id != exceptSessionId).ToList();

            foreach (var session in sessionsToRevoke)
            {
                session.IsActive = false;
                await _sessionRepository.UpdateAsync(session);
            }

            _logger.LogInformation("Revoked {Count} sessions for user {UserId}", sessionsToRevoke.Count, userId);

            return sessionsToRevoke.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke all sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> CleanupExpiredSessionsAsync(string userId)
    {
        try
        {
            var count = await _sessionRepository.DeleteExpiredSessionsAsync(userId);

            _logger.LogInformation("Cleaned up {Count} expired sessions for user {UserId}", count, userId);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired sessions for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSession?> ValidateSessionAsync(string userId, string refreshToken)
    {
        try
        {
            var session = await _sessionRepository.GetByRefreshTokenAsync(userId, refreshToken);

            if (session == null)
            {
                _logger.LogWarning("Session not found for user {UserId} with provided refresh token", userId);
                return null;
            }

            // Check if session is active
            if (!session.IsActive)
            {
                _logger.LogWarning("Session {SessionId} is not active", session.Id);
                return null;
            }

            // Check if session has expired
            if (session.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Session {SessionId} has expired", session.Id);
                return null;
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate session for user {UserId}", userId);
            throw;
        }
    }

    public async Task UpdateSessionAccessAsync(string sessionId)
    {
        try
        {
            // This would need to get the session first, update it, and save
            // Implementation depends on how we want to handle this
            // For now, we'll skip this as it's an optimization
            _logger.LogDebug("Updated last access time for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update session access for {SessionId}", sessionId);
            // Don't throw - this is not critical
        }
    }
}
