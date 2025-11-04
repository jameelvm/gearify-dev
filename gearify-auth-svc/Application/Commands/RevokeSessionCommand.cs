using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to revoke a specific session
/// </summary>
public record RevokeSessionCommand(string UserId, string SessionId) : IRequest<RevokeSessionResult>;

/// <summary>
/// Result of session revocation
/// </summary>
public record RevokeSessionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
