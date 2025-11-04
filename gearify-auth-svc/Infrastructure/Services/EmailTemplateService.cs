using Gearify.AuthService.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Gearify.AuthService.Infrastructure.Services;

/// <summary>
/// Service for loading and rendering email templates from HTML files
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly string _templatePath;
    private readonly Dictionary<string, string> _subjects;

    public EmailTemplateService(
        IHostEnvironment environment,
        ILogger<EmailTemplateService> logger)
    {
        _logger = logger;

        // Templates are stored in Infrastructure/EmailTemplates
        _templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EmailTemplates");

        // Define subjects for each template
        _subjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "WelcomeEmail", "Welcome to Gearify - Verify Your Email" },
            { "PasswordReset", "Reset Your Gearify Password" },
            { "EmailChanged", "Your Email Address Has Been Changed" },
            { "AccountLocked", "Security Alert: Your Account Has Been Locked" },
            { "AccountUnlocked", "Your Account Has Been Unlocked" },
            { "PasswordResetRequest", "Reset Your Gearify Password" },
            { "PasswordResetSuccess", "Password Reset Successful" },
            { "PasswordChanged", "Your Password Has Been Changed" },
            { "MfaEnabled", "Multi-Factor Authentication Enabled" },
            { "MfaDisabled", "Multi-Factor Authentication Disabled" }
        };

        // Ensure template directory exists
        if (!Directory.Exists(_templatePath))
        {
            _logger.LogWarning("Email template directory not found at {Path}", _templatePath);
        }
    }

    public async Task<EmailTemplateResult> RenderTemplateAsync(string templateName, Dictionary<string, string> data)
    {
        try
        {
            var htmlFile = Path.Combine(_templatePath, $"{templateName}.html");
            var textFile = Path.Combine(_templatePath, $"{templateName}.txt");

            if (!File.Exists(htmlFile))
            {
                _logger.LogError("HTML email template not found: {TemplateName} at {Path}", templateName, htmlFile);
                throw new FileNotFoundException($"HTML email template '{templateName}' not found", htmlFile);
            }

            // Read HTML template
            var htmlTemplate = await File.ReadAllTextAsync(htmlFile);
            var htmlBody = ReplaceePlaceholders(htmlTemplate, data);

            // Read text template (optional - if not found, generate from HTML)
            string textBody;
            if (File.Exists(textFile))
            {
                var textTemplate = await File.ReadAllTextAsync(textFile);
                textBody = ReplaceePlaceholders(textTemplate, data);
            }
            else
            {
                // Fallback: Generate simple text version from HTML by stripping tags
                _logger.LogWarning("Text template not found for {TemplateName}, generating from HTML", templateName);
                textBody = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<.*?>", string.Empty);
            }

            // Get subject
            var subject = GetSubject(templateName);

            return new EmailTemplateResult(subject, htmlBody, textBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render email template: {TemplateName}", templateName);
            throw;
        }
    }

    private string ReplaceePlaceholders(string template, Dictionary<string, string> data)
    {
        var rendered = template;
        foreach (var kvp in data)
        {
            var placeholder = $"{{{{{kvp.Key}}}}}"; // {{Key}} format
            rendered = rendered.Replace(placeholder, kvp.Value);
        }
        return rendered;
    }

    public string GetSubject(string templateName)
    {
        if (_subjects.TryGetValue(templateName, out var subject))
        {
            return subject;
        }

        _logger.LogWarning("Subject not found for template: {TemplateName}", templateName);
        return "Gearify Notification";
    }
}
