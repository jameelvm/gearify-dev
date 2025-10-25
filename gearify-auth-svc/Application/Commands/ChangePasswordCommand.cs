using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to change user password
/// </summary>
public record ChangePasswordCommand(
    string UserId,
    string CurrentPassword,
    string NewPassword
) : IRequest<ChangePasswordResult>;

/// <summary>
/// Result of password change operation
/// </summary>
public record ChangePasswordResult(
    bool Success,
    string? ErrorMessage = null
);
