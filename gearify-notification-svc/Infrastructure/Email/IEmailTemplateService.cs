using System.Collections.Generic;
using System.Threading.Tasks;

namespace Gearify.NotificationService.Infrastructure.Email;

/// <summary>
/// Result of rendering an email template
/// </summary>
public record EmailTemplateResult(string Subject, string HtmlBody, string TextBody);

/// <summary>
/// Service for loading and rendering email templates
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Renders an email template (both HTML and text versions) with the provided data
    /// </summary>
    /// <param name="templateName">Name of the template (e.g., "WelcomeEmail")</param>
    /// <param name="data">Template data as key-value pairs</param>
    /// <returns>Rendered email with subject, HTML body, and text body</returns>
    Task<EmailTemplateResult> RenderTemplateAsync(string templateName, Dictionary<string, string> data);

    /// <summary>
    /// Gets the subject line for a template
    /// </summary>
    /// <param name="templateName">Name of the template</param>
    /// <returns>Subject line</returns>
    string GetSubject(string templateName);
}
