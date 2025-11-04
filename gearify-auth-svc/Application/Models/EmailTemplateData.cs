namespace Gearify.AuthService.Application.Models;

/// <summary>
/// Base class for email template data
/// </summary>
public abstract class EmailTemplateData
{
    /// <summary>
    /// Converts the template data to a dictionary for rendering
    /// </summary>
    public abstract Dictionary<string, string> ToDictionary();
}

/// <summary>
/// Template data for welcome email
/// </summary>
public class WelcomeEmailData : EmailTemplateData
{
    public string FirstName { get; set; } = string.Empty;
    public string VerificationLink { get; set; } = string.Empty;

    public override Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            { "FirstName", FirstName },
            { "VerificationLink", VerificationLink }
        };
    }
}

/// <summary>
/// Template data for password reset email
/// </summary>
public class PasswordResetEmailData : EmailTemplateData
{
    public string FirstName { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;

    public override Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            { "FirstName", FirstName },
            { "ResetLink", ResetLink }
        };
    }
}
