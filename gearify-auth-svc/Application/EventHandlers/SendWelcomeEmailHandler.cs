using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Domain.Events;
using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.EventHandlers;

/// <summary>
/// Handles UserCreatedEvent by sending a welcome email with verification link
/// </summary>
public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SendWelcomeEmailHandler> _logger;

    public SendWelcomeEmailHandler(
        IEmailService emailService,
        IUserRepository userRepository,
        ILogger<SendWelcomeEmailHandler> logger)
    {
        _emailService = emailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Get user to retrieve verification token
            var user = await _userRepository.GetByIdAsync(notification.UserId, notification.TenantId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for sending welcome email", notification.UserId);
                return;
            }

            if (string.IsNullOrEmpty(user.EmailVerificationToken))
            {
                _logger.LogWarning("No verification token for user {UserId}", notification.UserId);
                return;
            }

            // Send welcome email with verification link
            await _emailService.SendWelcomeEmailAsync(
                notification.Email,
                notification.FirstName,
                user.EmailVerificationToken,
                cancellationToken
            );

            _logger.LogInformation(
                "Welcome email sent to {Email} for user {UserId}",
                notification.Email,
                notification.UserId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send welcome email to {Email} for user {UserId}",
                notification.Email,
                notification.UserId
            );
            // Don't throw - email failure shouldn't break registration
        }
    }
}
