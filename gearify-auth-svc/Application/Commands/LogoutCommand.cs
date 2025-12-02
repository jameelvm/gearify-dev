using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to logout by revoking the session associated with a refresh token
/// </summary>
public record LogoutCommand(
    string UserId,
    string RefreshToken
) : IRequest<LogoutResult>;

/// <summary>
/// Result of logout operation
/// </summary>
public record LogoutResult(
    bool Success,
    string? Message = null
);
