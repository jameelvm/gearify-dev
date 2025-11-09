using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles password change command
/// </summary>
public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        ITenantContext tenantContext,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ChangePasswordResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var user = await _repository.GetByIdAsync(request.UserId, tenantId);
            if (user == null)
            {
                _logger.LogWarning("Password change failed: User {UserId} not found", request.UserId);
                return new ChangePasswordResult(false, "User not found");
            }

            // Verify current password
            if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Password change failed: Invalid current password for user {UserId}", user.Id);
                return new ChangePasswordResult(false, "Current password is incorrect");
            }

            // Validate new password against policy
            var passwordValidation = _passwordPolicyService.ValidatePassword(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                var errorMessage = string.Join(" ", passwordValidation.Errors);
                _logger.LogWarning("Password change failed: Password does not meet policy requirements. {Errors}", errorMessage);
                return new ChangePasswordResult(false, errorMessage);
            }

            // Check if password is in history
            if (_passwordPolicyService.IsPasswordInHistory(request.NewPassword, user))
            {
                _logger.LogWarning("Password change failed: Password is in history for user {UserId}", user.Id);
                return new ChangePasswordResult(false, "You cannot reuse a recent password. Please choose a different password.");
            }

            // Add old password to history
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                _passwordPolicyService.AddToPasswordHistory(user.PasswordHash, user);
            }

            // Hash new password
            user.PasswordHash = _passwordPolicyService.HashPassword(request.NewPassword);
            user.LastPasswordChangeAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            // Invalidate refresh token for security
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _repository.UpdateAsync(user);

            _logger.LogInformation("Password changed successfully for user {UserId}", user.Id);

            return new ChangePasswordResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change password for user {UserId}", request.UserId);
            return new ChangePasswordResult(false, "Password change failed");
        }
    }
}
