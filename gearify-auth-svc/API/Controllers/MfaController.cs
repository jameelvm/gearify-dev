using Gearify.AuthService.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gearify.AuthService.API.Controllers;

/// <summary>
/// Controller for Multi-Factor Authentication operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MfaController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MfaController> _logger;

    public MfaController(IMediator mediator, ILogger<MfaController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Initiates TOTP (Authenticator App) MFA setup
    /// </summary>
    [HttpPost("setup/totp")]
    public async Task<IActionResult> SetupTotp()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new SetupTotpMfaCommand(userId);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, result.Message });
        }

        return Ok(new
        {
            result.Success,
            result.Message,
            result.QrCodeBase64,
            result.ManualEntryKey,
            result.BackupCodes
        });
    }

    /// <summary>
    /// Verifies and enables MFA after setup
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyMfaSetup([FromBody] VerifyMfaRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new VerifyMfaSetupCommand(userId, request.Code);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, result.Message });
        }

        return Ok(new { result.Success, result.Message });
    }

    /// <summary>
    /// Disables MFA for user
    /// </summary>
    [HttpPost("disable")]
    public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new DisableMfaCommand(userId, request.Password);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, result.Message });
        }

        return Ok(new { result.Success, result.Message });
    }
}

/// <summary>
/// Request model for MFA verification
/// </summary>
public record VerifyMfaRequest(string Code);

/// <summary>
/// Request model for disabling MFA
/// </summary>
public record DisableMfaRequest(string Password);
