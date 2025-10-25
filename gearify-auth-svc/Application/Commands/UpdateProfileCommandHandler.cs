using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles profile update command
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UpdateProfileResult>
{
    private readonly IUserRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        IUserRepository repository,
        ITenantContext tenantContext,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UpdateProfileResult> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var user = await _repository.GetByIdAsync(request.UserId, tenantId);
            if (user == null)
            {
                _logger.LogWarning("Profile update failed: User {UserId} not found", request.UserId);
                return new UpdateProfileResult(false, "User not found");
            }

            // Update profile fields if provided
            if (!string.IsNullOrWhiteSpace(request.FirstName))
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName))
            {
                user.LastName = request.LastName;
            }

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                user.Phone = request.Phone;
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            _logger.LogInformation("Profile updated successfully for user {UserId}", user.Id);

            return new UpdateProfileResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile for user {UserId}", request.UserId);
            return new UpdateProfileResult(false, "Profile update failed");
        }
    }
}
