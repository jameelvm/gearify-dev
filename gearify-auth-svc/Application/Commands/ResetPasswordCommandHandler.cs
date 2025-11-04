using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles password reset command
/// </summary>
public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IEmailService _emailService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserRepository repository,
        IPasswordPolicyService passwordPolicyService,
        IEmailService emailService,
        ITenantContext tenantContext,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _repository = repository;
        _passwordPolicyService = passwordPolicyService;
        _emailService = emailService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Find user by email
            var user = await _repository.GetByEmailAsync(request.Email.ToLowerInvariant(), tenantId);
            if (user == null)
            {
                _logger.LogWarning("Password reset failed: User not found with email {Email}", request.Email);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "Invalid reset token or email."
                };
            }

            // Validate reset token
            if (string.IsNullOrEmpty(user.PasswordResetToken) ||
                user.PasswordResetToken != request.ResetToken)
            {
                _logger.LogWarning("Password reset failed: Invalid token for user {UserId}", user.Id);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "Invalid reset token or email."
                };
            }

            // Check if token has expired
            if (user.PasswordResetTokenExpiry == null ||
                user.PasswordResetTokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Password reset failed: Expired token for user {UserId}", user.Id);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "Reset token has expired. Please request a new one."
                };
            }

            // Validate new password against policy
            var passwordValidation = _passwordPolicyService.ValidatePassword(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                var errorMessage = string.Join(" ", passwordValidation.Errors);
                _logger.LogWarning("Password reset failed: Password does not meet policy. {Errors}", errorMessage);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = errorMessage
                };
            }

            // Check if password is in history
            if (_passwordPolicyService.IsPasswordInHistory(request.NewPassword, user))
            {
                _logger.LogWarning("Password reset failed: Password is in history for user {UserId}", user.Id);
                return new ResetPasswordResult
                {
                    Success = false,
                    Message = "You cannot reuse a recent password. Please choose a different password."
                };
            }

            // Hash new password
            var newPasswordHash = _passwordPolicyService.HashPassword(request.NewPassword);

            // Add old password to history
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                _passwordPolicyService.AddToPasswordHistory(user.PasswordHash, user);
            }

            // Update user password
            user.PasswordHash = newPasswordHash;
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.LastPasswordChangeAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            // Reset lockout status
            user.FailedLoginAttempts = 0;
            user.LockoutEnd = null;

            // Invalidate existing refresh tokens for security
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _repository.UpdateAsync(user);

            // Send confirmation email
            var emailData = new Dictionary<string, string>
            {
                { "FirstName", user.FirstName },
                { "Email", user.Email },
                { "ChangeTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
            };

            await _emailService.SendTemplatedEmailAsync(user.Email, "PasswordResetSuccess", emailData);

            _logger.LogInformation("Password reset successful for user {UserId}", user.Id);

            return new ResetPasswordResult
            {
                Success = true,
                Message = "Your password has been reset successfully. You can now log in with your new password."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset password for {Email}", request.Email);
            return new ResetPasswordResult
            {
                Success = false,
                Message = "Failed to reset password. Please try again later."
            };
        }
    }
}
