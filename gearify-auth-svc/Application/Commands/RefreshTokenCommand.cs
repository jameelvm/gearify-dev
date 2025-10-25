using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to refresh an access token using a refresh token
/// </summary>
public record RefreshTokenCommand(
    string RefreshToken
) : IRequest<RefreshTokenResult>;

/// <summary>
/// Result of token refresh operation
/// </summary>
public record RefreshTokenResult(
    string Token,
    string RefreshToken,
    bool Success,
    string? ErrorMessage = null
);
