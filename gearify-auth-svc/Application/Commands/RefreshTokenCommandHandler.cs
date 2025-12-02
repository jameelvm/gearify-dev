using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles refresh token command
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IUserRepository _repository;
    private readonly IJwtService _jwtService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly ISessionService _sessionService;

    public RefreshTokenCommandHandler(
        IUserRepository repository,
        IJwtService jwtService,
        ITenantContext tenantContext,
        ILogger<RefreshTokenCommandHandler> logger,
        ISessionService sessionService)
    {
        _repository = repository;
        _jwtService = jwtService;
        _tenantContext = tenantContext;
        _logger = logger;
        _sessionService = sessionService;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Find user by refresh token (fallback for legacy tokens)
            var user = await _repository.GetByRefreshTokenAsync(request.RefreshToken, tenantId);
            if (user == null)
            {
                _logger.LogWarning("Refresh token validation failed: Token not found for tenant {TenantId}", tenantId);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Invalid refresh token");
            }

            // Validate session using refresh token
            var session = await _sessionService.ValidateSessionAsync(user.Id, request.RefreshToken);
            if (session == null)
            {
                _logger.LogWarning("Session validation failed for user {UserId} - session not found or expired", user.Id);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Invalid or expired session");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Refresh token failed: User {UserId} is inactive", user.Id);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Account is inactive");
            }

            // Clean up expired sessions for this user
            await _sessionService.CleanupExpiredSessionsAsync(user.Id);

            // Generate new tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            // Update user's refresh token (for backward compatibility)
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            // Update session with new refresh token and extend expiry
            session.RefreshToken = newRefreshToken;
            session.ExpiresAt = DateTime.UtcNow.AddDays(7);
            session.LastAccessedAt = DateTime.UtcNow;

            await _sessionService.RevokeSessionAsync(user.Id, session.Id); // Mark old session inactive

            // Create new session with rotated token
            await _sessionService.CreateSessionAsync(
                user.Id,
                user.TenantId,
                newRefreshToken,
                session.DeviceInfo,
                session.IpAddress,
                session.Location
            );

            _logger.LogInformation("Access token refreshed and session rotated for user {UserId}", user.Id);

            return new RefreshTokenResult(accessToken, newRefreshToken, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token for tenant {TenantId}", _tenantContext.TenantId);
            return new RefreshTokenResult(string.Empty, string.Empty, false, "Token refresh failed");
        }
    }
}
