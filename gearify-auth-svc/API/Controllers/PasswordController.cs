using Gearify.AuthService.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gearify.AuthService.API.Controllers;

/// <summary>
/// Controller for password management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PasswordController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PasswordController> _logger;

    public PasswordController(IMediator mediator, ILogger<PasswordController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Initiates password reset process
    /// </summary>
    [HttpPost("forgot")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var command = new ForgotPasswordCommand(request.Email);
        var result = await _mediator.Send(command);

        return Ok(new { result.Success, result.Message });
    }

    /// <summary>
    /// Resets password using reset token
    /// </summary>
    [HttpPost("reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.Email, request.ResetToken, request.NewPassword);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, result.Message });
        }

        return Ok(new { result.Success, result.Message });
    }

    /// <summary>
    /// Changes password for authenticated user
    /// </summary>
    [HttpPost("change")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { result.Success, Message = result.ErrorMessage });
        }

        return Ok(new { result.Success, Message = "Password changed successfully" });
    }
}

/// <summary>
/// Request model for forgot password
/// </summary>
public record ForgotPasswordRequest(string Email);

/// <summary>
/// Request model for reset password
/// </summary>
public record ResetPasswordRequest(string Email, string ResetToken, string NewPassword);

/// <summary>
/// Request model for change password
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
