using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Domain.Events;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles user registration command
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResult>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<RegisterUserCommandHandler> _logger;

    public RegisterUserCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<RegisterUserCommandHandler> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RegisterUserResult> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Check if user already exists
            var existingUser = await _repository.GetByEmailAsync(request.Email, tenantId);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: Email {Email} already exists for tenant {TenantId}", request.Email, tenantId);
                return new RegisterUserResult(string.Empty, string.Empty, string.Empty, false, "Email already registered");
            }

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Email = request.Email.ToLowerInvariant(),
                PasswordHash = _passwordHasher.HashPassword(request.Password),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone ?? string.Empty,
                Role = request.Role ?? "Customer",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                EmailVerified = false
            };

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // Store refresh token
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

            // Save user
            await _repository.CreateAsync(user);

            _logger.LogInformation("User registered successfully: {UserId} for tenant {TenantId}", user.Id, tenantId);

            // Publish event
            await _mediator.Publish(new UserCreatedEvent(
                user.Id,
                user.TenantId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role,
                user.CreatedAt
            ), cancellationToken);

            return new RegisterUserResult(user.Id, accessToken, refreshToken, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user for tenant {TenantId}", _tenantContext.TenantId);
            return new RegisterUserResult(string.Empty, string.Empty, string.Empty, false, "Registration failed");
        }
    }
}
