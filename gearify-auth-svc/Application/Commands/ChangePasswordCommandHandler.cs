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
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
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

            // Hash new password
            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
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
