using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to disable MFA for a user
/// </summary>
public record DisableMfaCommand(string UserId, string Password) : IRequest<DisableMfaResult>;

/// <summary>
/// Result of disable MFA operation
/// </summary>
public record DisableMfaResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
