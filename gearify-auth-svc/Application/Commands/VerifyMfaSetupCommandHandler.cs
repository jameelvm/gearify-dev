using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Enums;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles MFA setup verification and enablement
/// </summary>
public class VerifyMfaSetupCommandHandler : IRequestHandler<VerifyMfaSetupCommand, MfaVerificationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly ITotpService _totpService;
    private readonly IEmailService _emailService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<VerifyMfaSetupCommandHandler> _logger;

    public VerifyMfaSetupCommandHandler(
        IUserRepository userRepository,
        ITotpService totpService,
        IEmailService emailService,
        ITenantContext tenantContext,
        ILogger<VerifyMfaSetupCommandHandler> logger)
    {
        _userRepository = userRepository;
        _totpService = totpService;
        _emailService = emailService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<MfaVerificationResult> Handle(VerifyMfaSetupCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Get user
            var user = await _userRepository.GetByIdAsync(request.UserId, tenantId);
            if (user == null)
            {
                return new MfaVerificationResult
                {
                    Success = false,
                    Message = "User not found."
                };
            }

            // Check if user has a TOTP secret (from setup)
            if (string.IsNullOrEmpty(user.TotpSecret))
            {
                return new MfaVerificationResult
                {
                    Success = false,
                    Message = "MFA has not been set up. Please initiate setup first."
                };
            }

            // Verify the code
            var isValid = _totpService.VerifyCode(user.TotpSecret, request.Code);

            if (!isValid)
            {
                _logger.LogWarning("Invalid MFA verification code for user {UserId}", user.Id);
                return new MfaVerificationResult
                {
                    Success = false,
                    Message = "Invalid verification code. Please try again."
                };
            }

            // Enable MFA for the user
            user.MfaEnabled = true;
            user.PreferredMfaMethod = MfaMethod.Totp.ToString();
            user.LastMfaSetupAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            // Send confirmation email
            var emailData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "Method", "Authenticator App (TOTP)" },
                { "SetupTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
            };

            await _emailService.SendTemplatedEmailAsync(user.Email, "MfaEnabled", emailData);

            _logger.LogInformation("MFA enabled successfully for user {UserId}", user.Id);

            return new MfaVerificationResult
            {
                Success = true,
                Message = "MFA has been enabled successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify and enable MFA for user {UserId}", request.UserId);
            return new MfaVerificationResult
            {
                Success = false,
                Message = "Failed to enable MFA. Please try again later."
            };
        }
    }
}
