using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to reset user password with token
/// </summary>
public record ResetPasswordCommand(
    string Email,
    string ResetToken,
    string NewPassword
) : IRequest<ResetPasswordResult>;

/// <summary>
/// Result of password reset operation
/// </summary>
public record ResetPasswordResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
