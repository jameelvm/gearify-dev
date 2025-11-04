using Gearify.AuthService.API.DTOs;
using Gearify.AuthService.Application.Commands;
using Gearify.AuthService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gearify.AuthService.API.Controllers;

/// <summary>
/// Controller for authentication operations
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication response with tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var command = new RegisterUserCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Phone,
                request.Role
            );

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            var user = await _mediator.Send(new GetUserByIdQuery(result.UserId));
            if (user == null)
            {
                return StatusCode(500, new { error = "User created but not found" });
            }

            var response = new AuthResponse(
                result.Token,
                result.RefreshToken,
                new UserDto(
                    user.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.Phone,
                    user.Role,
                    user.IsActive,
                    user.EmailVerified,
                    user.LastLoginAt
                )
            );

            return CreatedAtAction(nameof(GetMe), null, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user registration");
            return StatusCode(500, new { error = "Registration failed" });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with tokens</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var command = new LoginCommand(request.Email, request.Password);
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return Unauthorized(new { error = result.ErrorMessage });
            }

            var response = new AuthResponse(
                result.Token,
                result.RefreshToken,
                new UserDto(
                    result.UserId,
                    result.Email,
                    result.FirstName,
                    result.LastName,
                    string.Empty,
                    result.Role,
                    true,
                    false,
                    DateTime.UtcNow
                )
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error = "Login failed" });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token</param>
    /// <returns>New access token and refresh token</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return Unauthorized(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                token = result.Token,
                refreshToken = result.RefreshToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { error = "Token refresh failed" });
        }
    }

    /// <summary>
    /// Verify email address using verification token
    /// </summary>
    /// <param name="token">Email verification token</param>
    /// <returns>Success or failure message</returns>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { error = "Verification token is required" });
            }

            var command = new VerifyEmailCommand(token);
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.Message });
            }

            return Ok(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during email verification");
            return StatusCode(500, new { error = "Email verification failed" });
        }
    }

    /// <summary>
    /// Get current authenticated user information
    /// </summary>
    /// <returns>Current user details</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "Invalid token" });
            }

            var user = await _mediator.Send(new GetUserByIdQuery(userId));
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            var userDto = new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Phone,
                user.Role,
                user.IsActive,
                user.EmailVerified,
                user.LastLoginAt
            );

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, new { error = "Failed to retrieve user" });
        }
    }
}
