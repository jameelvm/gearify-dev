using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Queries;

/// <summary>
/// Handles getting active sessions for a user
/// </summary>
public class GetActiveSessionsQueryHandler : IRequestHandler<GetActiveSessionsQuery, List<SessionInfo>>
{
    private readonly ISessionService _sessionService;
    private readonly ILogger<GetActiveSessionsQueryHandler> _logger;

    public GetActiveSessionsQueryHandler(
        ISessionService sessionService,
        ILogger<GetActiveSessionsQueryHandler> logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<List<SessionInfo>> Handle(GetActiveSessionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _sessionService.GetActiveSessionsAsync(request.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active sessions for user {UserId}", request.UserId);
            return new List<SessionInfo>();
        }
    }
}
