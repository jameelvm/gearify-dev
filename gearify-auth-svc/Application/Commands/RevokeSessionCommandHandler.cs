using Gearify.AuthService.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles session revocation
/// </summary>
public class RevokeSessionCommandHandler : IRequestHandler<RevokeSessionCommand, RevokeSessionResult>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<RevokeSessionCommandHandler> _logger;

    public RevokeSessionCommandHandler(
        ISessionService sessionService,
        ILogger<RevokeSessionCommandHandler> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<RevokeSessionResult> Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _sessionService.RevokeSessionAsync(request.UserId, request.SessionId);

            return new RevokeSessionResult
            {
                Success = success,
                Message = success ? "Session revoked successfully." : "Session not found."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session {SessionId} for user {UserId}", request.SessionId, request.UserId);
            return new RevokeSessionResult
            {
                Success = false,
                Message = "Failed to revoke session. Please try again later."
            };
        }
    }
}
