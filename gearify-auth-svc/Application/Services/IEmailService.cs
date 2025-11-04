namespace Gearify.AuthService.Application.Services;

/// <summary>
/// Service for sending emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a welcome email with verification link to a new user
    /// </summary>
    Task SendWelcomeEmailAsync(string email, string firstName, string verificationToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a generic email
    /// </summary>
    Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a templated email with placeholder data
    /// </summary>
    Task SendTemplatedEmailAsync(string to, string templateName, Dictionary<string, string> templateData, CancellationToken cancellationToken = default);
}
