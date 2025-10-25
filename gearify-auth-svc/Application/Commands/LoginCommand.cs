using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to authenticate a user and generate tokens
/// </summary>
public record LoginCommand(
    string Email,
    string Password
) : IRequest<LoginResult>;

/// <summary>
/// Result of login operation
/// </summary>
public record LoginResult(
    string UserId,
    string Token,
    string RefreshToken,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool Success,
    string? ErrorMessage = null
);
