using Gearify.AuthService.Application.Models;
using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to set up TOTP (Authenticator App) MFA
/// </summary>
public record SetupTotpMfaCommand(string UserId) : IRequest<MfaSetupResult>;
