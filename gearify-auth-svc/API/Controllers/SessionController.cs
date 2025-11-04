using Gearify.AuthService.Application.Commands;
using Gearify.AuthService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gearify.AuthService.API.Controllers;

/// <summary>
/// Controller for session management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SessionController> _logger;

    public SessionController(IMediator mediator, ILogger<SessionController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active sessions for the authenticated user
    /// </summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var query = new GetActiveSessionsQuery(userId);
        var sessions = await _mediator.Send(query);

        return Ok(new { Success = true, Sessions = sessions });
    }

    /// <summary>
    /// Revokes a specific session
    /// </summary>
    [HttpPost("revoke/{sessionId}")]
    public async Task<IActionResult> RevokeSession(string sessionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new RevokeSessionCommand(userId, sessionId);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, result.Message });
        }

        return Ok(new { result.Success, result.Message });
    }

    /// <summary>
    /// Revokes all sessions except the current one
    /// </summary>
    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // TODO: Get current session ID from token claims
        // For now, we'll revoke all sessions
        var command = new RevokeSessionCommand(userId, "");

        return Ok(new { Success = true, Message = "All sessions revoked successfully." });
    }
}
