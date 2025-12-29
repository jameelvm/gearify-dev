using Gearify.AuthService.Application.Services;

namespace Gearify.AuthService.Infrastructure.Clients;

/// <summary>
/// HTTP client for calling the Notification Service
/// Implements IEmailService to maintain compatibility with existing code
/// </summary>
public class NotificationServiceClient : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationServiceClient> _logger;

    public NotificationServiceClient(
        HttpClient httpClient,
        ILogger<NotificationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendWelcomeEmailAsync(
        string email,
        string firstName,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                Email = email,
                FirstName = firstName,
                VerificationToken = verificationToken
            };

            var response = await _httpClient.PostAsJsonAsync("/api/email/welcome", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Welcome email request sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling notification service to send welcome email to {Email}", email);
            // Don't throw - email failure shouldn't break registration
        }
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                To = to,
                Subject = subject,
                Body = body
            };

            var response = await _httpClient.PostAsJsonAsync("/api/email/send", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Email request sent successfully to {Email}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling notification service to send email to {Email}", to);
            // Don't throw - email failure shouldn't break the operation
        }
    }

    public async Task SendTemplatedEmailAsync(
        string to,
        string templateName,
        Dictionary<string, string> templateData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                To = to,
                TemplateName = templateName,
                TemplateData = templateData
            };

            var response = await _httpClient.PostAsJsonAsync("/api/email/templated", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Templated email request sent successfully to {Email} using template {Template}", to, templateName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling notification service to send templated email to {Email}", to);
            // Don't throw - email failure shouldn't break the operation
        }
    }
}
