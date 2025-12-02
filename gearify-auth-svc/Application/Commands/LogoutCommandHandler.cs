using Gearify.AuthService.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles logout command by finding and revoking the session
/// </summary>
public class LogoutCommandHandler : IRequestHandler<LogoutCommand, LogoutResult>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        ISessionService sessionService,
        ILogger<LogoutCommandHandler> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<LogoutResult> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the session by refresh token
            var session = await _sessionService.ValidateSessionAsync(request.UserId, request.RefreshToken);

            if (session == null)
            {
                _logger.LogWarning("Logout failed: Session not found for user {UserId}", request.UserId);
                return new LogoutResult(false, "Session not found");
            }

            // Revoke the session
            var revoked = await _sessionService.RevokeSessionAsync(request.UserId, session.Id);

            if (revoked)
            {
                _logger.LogInformation("User {UserId} logged out from session {SessionId}", request.UserId, session.Id);
                return new LogoutResult(true, "Logged out successfully");
            }

            return new LogoutResult(false, "Failed to revoke session");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to logout user {UserId}", request.UserId);
            return new LogoutResult(false, "Logout failed");
        }
    }
}
