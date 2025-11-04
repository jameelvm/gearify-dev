using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles forgot password command
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;
    private readonly IConfiguration _configuration;
    private readonly PasswordResetSettings _resetSettings;

    public ForgotPasswordCommandHandler(
        IUserRepository repository,
        IEmailService emailService,
        ITenantContext tenantContext,
        ILogger<ForgotPasswordCommandHandler> logger,
        IConfiguration configuration,
        IOptions<SecurityConfiguration> securityConfig)
    {
        _repository = repository;
        _emailService = emailService;
        _tenantContext = tenantContext;
        _logger = logger;
        _configuration = configuration;
        _resetSettings = securityConfig.Value.PasswordReset;
    }

    public async Task<ForgotPasswordResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Find user by email
            var user = await _repository.GetByEmailAsync(request.Email.ToLowerInvariant(), tenantId);

            // Always return success to prevent user enumeration
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Password reset requested for non-existent or inactive user: {Email}", request.Email);
                return new ForgotPasswordResult
                {
                    Success = true,
                    Message = "If an account exists with this email, you will receive a password reset link."
                };
            }

            // Generate secure reset token
            var resetToken = GenerateSecureToken();
            var resetExpiry = DateTime.UtcNow.AddHours(_resetSettings.TokenExpiryHours);

            // Update user with reset token
            user.PasswordResetToken = resetToken;
            user.PasswordResetTokenExpiry = resetExpiry;
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            // Send password reset email
            var webAppUrl = _configuration["WebAppUrl"] ?? "http://localhost:4200";
            var resetLink = $"{webAppUrl}/reset-password?token={resetToken}&email={Uri.EscapeDataString(user.Email)}";

            var emailData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "ResetLink", resetLink },
                { "ExpiryHours", _resetSettings.TokenExpiryHours.ToString() },
                { "Email", user.Email }
            };

            await _emailService.SendTemplatedEmailAsync(user.Email, "PasswordResetRequest", emailData);

            _logger.LogInformation("Password reset email sent to user {UserId}", user.Id);

            return new ForgotPasswordResult
            {
                Success = true,
                Message = "If an account exists with this email, you will receive a password reset link."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process forgot password request for {Email}", request.Email);
            return new ForgotPasswordResult
            {
                Success = false,
                Message = "Failed to process password reset request. Please try again later."
            };
        }
    }

    private static string GenerateSecureToken()
    {
        // Generate a cryptographically secure random token
        var randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
