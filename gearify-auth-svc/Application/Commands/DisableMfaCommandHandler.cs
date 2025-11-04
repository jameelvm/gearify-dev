using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Enums;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles disabling MFA
/// </summary>
public class DisableMfaCommandHandler : IRequestHandler<DisableMfaCommand, DisableMfaResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IEmailService _emailService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DisableMfaCommandHandler> _logger;

    public DisableMfaCommandHandler(
        IUserRepository userRepository,
        IPasswordPolicyService passwordPolicyService,
        IEmailService emailService,
        ITenantContext tenantContext,
        ILogger<DisableMfaCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordPolicyService = passwordPolicyService;
        _emailService = emailService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DisableMfaResult> Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Get user
            var user = await _userRepository.GetByIdAsync(request.UserId, tenantId);
            if (user == null)
            {
                return new DisableMfaResult
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            // Verify password for security
            if (!_passwordPolicyService.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Invalid password when attempting to disable MFA for user {UserId}", user.Id);
                return new DisableMfaResult
                {
                    Success = false,
                    Message = "Invalid password."
                };
            }

            // Check if MFA is enabled
            if (!user.MfaEnabled)
            {
                return new DisableMfaResult
                {
                    Success = false,
                    Message = "MFA is not currently enabled."
                };
            }

            // Disable MFA
            user.MfaEnabled = false;
            user.PreferredMfaMethod = MfaMethod.None.ToString();
            user.TotpSecret = null;
            user.BackupCodes = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            // Send confirmation email
            var emailData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "DisabledTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
            };

            await _emailService.SendTemplatedEmailAsync(user.Email, "MfaDisabled", emailData);

            _logger.LogInformation("MFA disabled for user {UserId}", user.Id);

            return new DisableMfaResult
            {
                Success = true,
                Message = "MFA has been disabled successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable MFA for user {UserId}", request.UserId);
            return new DisableMfaResult
            {
                Success = false,
                Message = "Failed to disable MFA. Please try again later."
            };
        }
    }
}
