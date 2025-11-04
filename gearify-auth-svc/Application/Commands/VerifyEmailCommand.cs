using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to verify user's email address
/// </summary>
public record VerifyEmailCommand(string Token) : IRequest<VerifyEmailResult>;

/// <summary>
/// Result of email verification
/// </summary>
public record VerifyEmailResult(bool Success, string Message);
