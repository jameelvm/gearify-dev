using Gearify.AuthService.Application.Models;
using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to verify and enable MFA after setup
/// </summary>
public record VerifyMfaSetupCommand(
    string UserId,
    string Code
) : IRequest<MfaVerificationResult>;
