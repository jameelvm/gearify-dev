using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles refresh token command
/// </summary>
public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IUserRepository _repository;
    private readonly IJwtService _jwtService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserRepository repository,
        IJwtService jwtService,
        ITenantContext tenantContext,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _repository = repository;
        _jwtService = jwtService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Find user by refresh token
            var user = await _repository.GetByRefreshTokenAsync(request.RefreshToken, tenantId);
            if (user == null)
            {
                _logger.LogWarning("Refresh token validation failed: Token not found for tenant {TenantId}", tenantId);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Invalid refresh token");
            }

            // Check if token is expired
            if (user.RefreshTokenExpiry == null || user.RefreshTokenExpiry < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token expired for user {UserId}", user.Id);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Refresh token expired");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Refresh token failed: User {UserId} is inactive", user.Id);
                return new RefreshTokenResult(string.Empty, string.Empty, false, "Account is inactive");
            }

            // Generate new tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var newRefreshToken = _jwtService.GenerateRefreshToken();

            // Update user's refresh token
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            _logger.LogInformation("Access token refreshed for user {UserId}", user.Id);

            return new RefreshTokenResult(accessToken, newRefreshToken, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token for tenant {TenantId}", _tenantContext.TenantId);
            return new RefreshTokenResult(string.Empty, string.Empty, false, "Token refresh failed");
        }
    }
}
