using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to initiate password reset process
/// </summary>
public record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResult>;

/// <summary>
/// Result of forgot password operation
/// </summary>
public record ForgotPasswordResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
