using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to register a new user
/// </summary>
public record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    string? Role
) : IRequest<RegisterUserResult>;

/// <summary>
/// Result of user registration
/// </summary>
public record RegisterUserResult(
    string UserId,
    string Token,
    string RefreshToken,
    bool Success,
    string? ErrorMessage = null
);
