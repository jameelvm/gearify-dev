using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Events;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IAccountLockoutService _accountLockoutService;
    private readonly IEmailService _emailService;
    private readonly ISessionService _sessionService;
    private readonly SessionSettings _sessionSettings;

    public LoginCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<LoginCommandHandler> logger,
        IAccountLockoutService accountLockoutService,
        IEmailService emailService,
        ISessionService sessionService,
        IOptionsSnapshot<SecurityConfiguration> securityConfig)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
        _accountLockoutService = accountLockoutService;
        _emailService = emailService;
        _sessionService = sessionService;
        _sessionSettings = securityConfig.Value.Session;
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

            // Check if account is locked out
            if (_accountLockoutService.IsLockedOut(user))
            {
                var remainingTime = _accountLockoutService.GetRemainingLockoutTime(user);
                _logger.LogWarning("Login failed: Account {UserId} is locked out. Remaining time: {RemainingMinutes} minutes",
                    user.Id, remainingTime?.TotalMinutes ?? 0);

                return new LoginResult(
                    string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    false,
                    $"Account is locked. Please try again in {Math.Ceiling(remainingTime?.TotalMinutes ?? 0)} minutes."
                );
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

                // Record failed attempt and check if account should be locked
                var wasLocked = _accountLockoutService.RecordFailedLoginAttempt(user);
                await _repository.UpdateAsync(user);

                if (wasLocked)
                {
                    _logger.LogWarning("Account {UserId} has been locked due to too many failed attempts", user.Id);

                    // Send account locked email
                    var lockoutData = new Dictionary<string, string>
                    {
                        { "FirstName", user.FirstName },
                        { "FailedAttempts", user.FailedLoginAttempts.ToString() },
                        { "LockoutTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                        { "UnlockTime", user.LockoutEnd?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "Unknown" },
                        { "LockoutDuration", _accountLockoutService.GetRemainingLockoutTime(user)?.TotalMinutes.ToString("0") ?? "30" },
                        { "ResetPasswordLink", $"{GetWebAppUrl()}/reset-password" }
                    };

                    await _emailService.SendTemplatedEmailAsync(user.Email, "AccountLocked", lockoutData);

                    var remainingTime = _accountLockoutService.GetRemainingLockoutTime(user);
                    return new LoginResult(
                        string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                        false,
                        $"Too many failed attempts. Account is locked for {Math.Ceiling(remainingTime?.TotalMinutes ?? 30)} minutes."
                    );
                }

                return new LoginResult(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, "Invalid email or password");
            }

            // Password is correct - reset failed login attempts
            _accountLockoutService.ResetFailedLoginAttempts(user);

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Update user's last login and refresh token
            user.LastLoginAt = DateTime.UtcNow;
            user.RefreshToken = refreshToken;

            // Set refresh token expiry based on remember me option
            var expiryDays = request.RememberMe
                ? _sessionSettings.RefreshTokenExpiryDaysRememberMe
                : _sessionSettings.RefreshTokenExpiryDays;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(expiryDays);
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            // Create session for multi-device tracking
            var deviceInfo = request.DeviceInfo ?? "Unknown Device";
            var ipAddress = request.IpAddress ?? "Unknown IP";

            await _sessionService.CreateSessionAsync(
                user.Id,
                user.TenantId,
                refreshToken,
                deviceInfo,
                ipAddress
            );

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

    private string GetWebAppUrl()
    {
        // This should come from configuration - using placeholder for now
        return "http://localhost:4200";
    }
}
