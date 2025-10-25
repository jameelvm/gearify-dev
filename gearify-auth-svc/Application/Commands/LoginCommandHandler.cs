using Gearify.AuthService.Domain.Events;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles user login command
/// </summary>
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<LoginCommandHandler> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Find user by email
            var user = await _repository.GetByEmailAsync(request.Email.ToLowerInvariant(), tenantId);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found with email {Email} for tenant {TenantId}", request.Email, tenantId);
                return new LoginResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Invalid email or password");
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: User {UserId} is inactive", user.Id);
                return new LoginResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Account is inactive");
            }

            // Verify password
            if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for user {UserId}", user.Id);
                return new LoginResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Invalid email or password");
            }

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Update user's last login and refresh token
            user.LastLoginAt = DateTime.UtcNow;
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            _logger.LogInformation("User logged in successfully: {UserId}", user.Id);

            // Publish event
            await _mediator.Publish(new UserLoggedInEvent(
                user.Id,
                user.TenantId,
                user.Email,
                user.LastLoginAt.Value
            ), cancellationToken);

            return new LoginResult(
                user.Id,
                accessToken,
                refreshToken,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
                true
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login user for tenant {TenantId}", _tenantContext.TenantId);
            return new LoginResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Login failed");
        }
    }
}
