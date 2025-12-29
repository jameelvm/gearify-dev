using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.NotificationService.Infrastructure.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.NotificationService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailService emailService,
        ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Sends a welcome email with verification link
    /// </summary>
    [HttpPost("welcome")]
    public async Task<IActionResult> SendWelcomeEmail([FromBody] SendWelcomeEmailRequest request)
    {
        try
        {
            await _emailService.SendWelcomeEmailAsync(
                request.Email,
                request.FirstName,
                request.VerificationToken,
                HttpContext.RequestAborted);

            return Ok(new { success = true, message = "Welcome email sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending welcome email to {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Failed to send welcome email" });
        }
    }

    /// <summary>
    /// Sends a generic email
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            await _emailService.SendEmailAsync(
                request.To,
                request.Subject,
                request.Body,
                HttpContext.RequestAborted);

            return Ok(new { success = true, message = "Email sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Email}", request.To);
            return StatusCode(500, new { success = false, message = "Failed to send email" });
        }
    }

    /// <summary>
    /// Sends a templated email
    /// </summary>
    [HttpPost("templated")]
    public async Task<IActionResult> SendTemplatedEmail([FromBody] SendTemplatedEmailRequest request)
    {
        try
        {
            await _emailService.SendTemplatedEmailAsync(
                request.To,
                request.TemplateName,
                request.TemplateData,
                HttpContext.RequestAborted);

            return Ok(new { success = true, message = "Templated email sent successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending templated email to {Email}", request.To);
            return StatusCode(500, new { success = false, message = "Failed to send templated email" });
        }
    }
}

// Request DTOs
public record SendWelcomeEmailRequest(string Email, string FirstName, string VerificationToken);
public record SendEmailRequest(string To, string Subject, string Body);
public record SendTemplatedEmailRequest(string To, string TemplateName, Dictionary<string, string> TemplateData);
