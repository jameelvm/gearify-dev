using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Gearify.AuthService.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Implementation of email service using AWS SES with template support
/// Works with LocalStack for local development and real AWS SES in production
/// </summary>
public class SesEmailService : IEmailService
{
    private readonly IAmazonSimpleEmailService _sesClient;
    private readonly IEmailTemplateService _templateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SesEmailService> _logger;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _webAppUrl;

    public SesEmailService(
        IAmazonSimpleEmailService sesClient,
        IEmailTemplateService templateService,
        IConfiguration configuration,
        ILogger<SesEmailService> logger)
    {
        _sesClient = sesClient;
        _templateService = templateService;
        _configuration = configuration;
        _logger = logger;

        // Read email settings from configuration
        _fromEmail = configuration["Email:FromEmail"] ?? "noreply@gearify.com";
        _fromName = configuration["Email:FromName"] ?? "Gearify";
        _webAppUrl = configuration["WebAppUrl"] ?? "http://localhost:4200";
    }

    public async Task SendWelcomeEmailAsync(
        string email,
        string firstName,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var verificationLink = $"{_webAppUrl}/verify-email?token={verificationToken}";

        // Prepare template data
        var templateData = new Dictionary<string, string>
        {
            { "FirstName", firstName },
            { "VerificationLink", verificationLink }
        };

        // Render template (both HTML and text)
        var template = await _templateService.RenderTemplateAsync("WelcomeEmail", templateData);

        await SendEmailAsync(email, template.Subject, template.HtmlBody, template.TextBody, cancellationToken);
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        // For the generic method, use the HTML body and generate a simple text version
        var textBody = System.Text.RegularExpressions.Regex.Replace(body, "<.*?>", string.Empty);
        await SendEmailAsync(to, subject, body, textBody, cancellationToken);
    }

    public async Task SendTemplatedEmailAsync(
        string to,
        string templateName,
        Dictionary<string, string> templateData,
        CancellationToken cancellationToken = default)
    {
        // Render template (both HTML and text)
        var template = await _templateService.RenderTemplateAsync(templateName, templateData);

        await SendEmailAsync(to, template.Subject, template.HtmlBody, template.TextBody, cancellationToken);
    }

    private async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sendRequest = new SendEmailRequest
            {
                Source = $"{_fromName} <{_fromEmail}>",
                Destination = new Destination
                {
                    ToAddresses = new List<string> { to }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = htmlBody
                        },
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = textBody
                        }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(sendRequest, cancellationToken);

            _logger.LogInformation(
                "Email sent successfully to {Email} via SES. MessageId: {MessageId}",
                to,
                response.MessageId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} via SES", to);
            // Don't throw - email failure shouldn't break registration
        }
    }
}
