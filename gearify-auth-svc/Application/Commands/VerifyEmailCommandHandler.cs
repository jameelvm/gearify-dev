using Gearify.AuthService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Handles email verification command
/// </summary>
public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailResult>
{
    private readonly IUserRepository _repository;
    private readonly ILogger<VerifyEmailCommandHandler> _logger;

    public VerifyEmailCommandHandler(
        IUserRepository repository,
        ILogger<VerifyEmailCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<VerifyEmailResult> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find user by verification token
            var user = await _repository.GetByEmailVerificationTokenAsync(request.Token);

            if (user == null)
            {
                _logger.LogWarning("Invalid or expired verification token");
                return new VerifyEmailResult(false, "Invalid or expired verification token");
            }

            // Check if token is expired
            if (user.EmailVerificationTokenExpiry.HasValue &&
                user.EmailVerificationTokenExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Verification token expired for user {UserId}", user.Id);
                return new VerifyEmailResult(false, "Verification token has expired");
            }

            // Check if already verified
            if (user.EmailVerified)
            {
                _logger.LogInformation("User {UserId} email already verified", user.Id);
                return new VerifyEmailResult(true, "Email already verified");
            }

            // Mark email as verified
            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(user);

            _logger.LogInformation("Email verified successfully for user {UserId}", user.Id);

            return new VerifyEmailResult(true, "Email verified successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify email");
            return new VerifyEmailResult(false, "Email verification failed");
        }
    }
}
