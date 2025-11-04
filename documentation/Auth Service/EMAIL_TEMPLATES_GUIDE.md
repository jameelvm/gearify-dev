# Email Templates Guide

This guide explains how to create and manage email templates in the Gearify auth service.

## Overview

The email system uses **HTML template files** with placeholder replacement, making it easy to:
- ✅ Separate email design from business logic
- ✅ Edit templates without code changes
- ✅ Preview templates in a browser
- ✅ Maintain consistent branding
- ✅ Support multiple languages (future)

## Architecture

### Components

1. **IEmailTemplateService**: Interface for template operations
2. **EmailTemplateService**: Loads and renders templates from HTML files
3. **SesEmailService**: Sends emails using rendered templates
4. **Template Files**: HTML files in `Infrastructure/EmailTemplates/`

### Template Flow

```
Template File (.html)
    ↓
EmailTemplateService.RenderTemplateAsync()
    ↓ (Replace {{placeholders}})
Rendered HTML
    ↓
SesEmailService.SendEmailAsync()
    ↓
AWS SES → User's Inbox
```

## Creating a New Email Template

### Step 1: Create Both HTML and Text Template Files

Create **two files** in `Infrastructure/EmailTemplates/`:
1. `TemplateName.html` - HTML version (for modern email clients)
2. `TemplateName.txt` - Plain text version (for text-only email clients)

**Example**: `Infrastructure/EmailTemplates/PasswordReset.html`

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Reset Your Password</title>
</head>
<body style="font-family: Arial, sans-serif; padding: 20px;">
    <h2>Hello {{FirstName}},</h2>
    <p>Click the button below to reset your password:</p>
    <a href="{{ResetLink}}" style="background-color: #4CAF50; color: white; padding: 12px 24px;">
        Reset Password
    </a>
</body>
</html>
```

**Key Points**:
- Use `{{PlaceholderName}}` for dynamic content
- Use inline styles (email clients don't support external CSS)
- Keep it simple and compatible with email clients

**Example**: `Infrastructure/EmailTemplates/PasswordReset.txt`

```text
═══════════════════════════════════════════════════════════════
                  PASSWORD RESET REQUEST
═══════════════════════════════════════════════════════════════

Hello {{FirstName}},

We received a request to reset your password.

Click the link below to reset your password:

{{ResetLink}}

⚠️  IMPORTANT: This link expires in 1 hour.

Best regards,
The Gearify Team
```

**Key Points**:
- Use the same `{{Placeholders}}` as in HTML version
- Use ASCII art for visual separation (═, ─, etc.)
- Keep line length under 72 characters for compatibility
- If `.txt` file is missing, it will auto-generate from HTML (not recommended)

### Step 2: Add Subject Line to EmailTemplateService

Update `Infrastructure/Services/EmailTemplateService.cs`:

```csharp
_subjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "WelcomeEmail", "Welcome to Gearify - Verify Your Email" },
    { "PasswordReset", "Reset Your Gearify Password" },  // Add this
    { "EmailChanged", "Your Email Address Has Been Changed" }
};
```

### Step 3: Create Template Data Model (Optional)

For type safety, create a data model in `Application/Models/EmailTemplateData.cs`:

```csharp
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
```

### Step 4: Use the Template in Your Code

```csharp
// In your command handler or service
var resetLink = $"{_webAppUrl}/reset-password?token={resetToken}";

var templateData = new Dictionary<string, string>
{
    { "FirstName", user.FirstName },
    { "ResetLink", resetLink }
};

var htmlBody = await _templateService.RenderTemplateAsync("PasswordReset", templateData);
var subject = _templateService.GetSubject("PasswordReset");

await _emailService.SendEmailAsync(user.Email, subject, htmlBody);
```

## Available Templates

### 1. WelcomeEmail.html

**Purpose**: Welcome email with email verification link

**Placeholders**:
- `{{FirstName}}`: User's first name
- `{{VerificationLink}}`: Email verification URL

**Usage**:
```csharp
await _emailService.SendWelcomeEmailAsync(email, firstName, verificationToken);
```

### 2. PasswordReset.html

**Purpose**: Password reset email (example template)

**Placeholders**:
- `{{FirstName}}`: User's first name
- `{{ResetLink}}`: Password reset URL

**Usage**:
```csharp
var data = new Dictionary<string, string>
{
    { "FirstName", "John" },
    { "ResetLink", "https://app.gearify.com/reset?token=abc123" }
};
var html = await _templateService.RenderTemplateAsync("PasswordReset", data);
```

## Template Best Practices

### 1. Design for Email Clients

Email clients have limited CSS support. Follow these rules:

✅ **DO**:
- Use inline styles (`style="..."`)
- Use tables for layout
- Use web-safe fonts (Arial, Helvetica, sans-serif)
- Test in multiple email clients
- Include alt text for images
- Keep width under 600px

❌ **DON'T**:
- Use external CSS files
- Use JavaScript
- Rely on `<div>` for layout
- Use CSS Grid or Flexbox
- Use background images (limited support)

### 2. Responsive Design

Use media queries for mobile support:

```html
<style>
    @media only screen and (max-width: 600px) {
        .container {
            width: 100% !important;
        }
    }
</style>
```

### 3. Accessibility

- Use semantic HTML
- Include alt text for images
- Ensure sufficient color contrast
- Use heading tags properly

### 4. Testing

Test your templates:
- Gmail (web, iOS, Android)
- Outlook (Windows, Mac, Office 365)
- Apple Mail (iOS, macOS)
- Yahoo Mail
- ProtonMail

Tools for testing:
- [Litmus](https://litmus.com/)
- [Email on Acid](https://www.emailonacid.com/)
- [Mailtrap](https://mailtrap.io/)

## Template Structure

### Recommended Layout

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Email Subject</title>
</head>
<body style="margin: 0; padding: 0; background-color: #f4f4f4;">
    <table role="presentation" style="width: 100%; border-collapse: collapse;">
        <tr>
            <td style="padding: 40px 0;">
                <!-- Main Container (600px wide) -->
                <table role="presentation" style="width: 600px; margin: 0 auto; background-color: #ffffff;">

                    <!-- Header -->
                    <tr>
                        <td style="padding: 40px; background-color: #667eea;">
                            <h1 style="color: #ffffff;">Your Brand</h1>
                        </td>
                    </tr>

                    <!-- Content -->
                    <tr>
                        <td style="padding: 40px;">
                            <!-- Your content here -->
                            <p>{{DynamicContent}}</p>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style="padding: 20px; background-color: #f8f9fa;">
                            <p style="font-size: 12px; color: #999999;">
                                © 2024 Your Company. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>
```

## Placeholder Syntax

Use double curly braces: `{{PlaceholderName}}`

**Examples**:
```html
<h2>Hello {{FirstName}}!</h2>
<a href="{{ActionLink}}">Click here</a>
<p>Your order #{{OrderNumber}} has shipped.</p>
```

**Case Sensitivity**: Placeholders are case-sensitive!
- ✅ `{{FirstName}}` matches `{ "FirstName": "John" }`
- ❌ `{{firstname}}` does NOT match `{ "FirstName": "John" }`

## Adding Images

### Option 1: Inline Images (Base64)

```html
<img src="data:image/png;base64,iVBORw0KGgoAAAANS..." alt="Logo">
```

**Pros**: Always displays
**Cons**: Large file size, not recommended for email

### Option 2: External Images (Recommended)

```html
<img src="https://yourdomain.com/images/logo.png" alt="Gearify Logo" width="200">
```

**Pros**: Small email size, easy to update
**Cons**: Requires hosting, might be blocked

**Best Practice**: Host images on a CDN (CloudFront, Cloudinary, etc.)

## Localization / Multiple Languages

To support multiple languages:

### Option 1: Separate Template Files

```
EmailTemplates/
  ├── WelcomeEmail.en.html
  ├── WelcomeEmail.es.html
  └── WelcomeEmail.fr.html
```

Update `EmailTemplateService`:
```csharp
var templateFile = $"{templateName}.{language}.html";
```

### Option 2: Placeholder Translation

Keep one template, translate placeholders:

```csharp
var data = new Dictionary<string, string>
{
    { "Greeting", Resources.Strings.Greeting[language] },
    { "FirstName", user.FirstName }
};
```

## Troubleshooting

### Template Not Found

**Error**: `Email template 'WelcomeEmail' not found`

**Solutions**:
1. Check file exists: `Infrastructure/EmailTemplates/WelcomeEmail.html`
2. Verify `.csproj` includes:
   ```xml
   <ItemGroup>
     <None Update="Infrastructure\EmailTemplates\*.html">
       <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
     </None>
   </ItemGroup>
   ```
3. Rebuild project: `dotnet build`

### Placeholders Not Replaced

**Issue**: Template shows `{{FirstName}}` instead of "John"

**Solutions**:
1. Check placeholder name matches exactly (case-sensitive)
2. Verify data dictionary contains the key
3. Check for typos in placeholder syntax

### Styling Not Working

**Issue**: Styles don't appear in email

**Solutions**:
1. Use inline styles, not `<style>` tags or external CSS
2. Use table-based layout instead of `<div>`
3. Test in actual email clients, not just browsers

## Performance Considerations

### Caching Templates

For better performance, cache rendered templates:

```csharp
public class CachedEmailTemplateService : IEmailTemplateService
{
    private readonly IMemoryCache _cache;

    public async Task<string> RenderTemplateAsync(string templateName, Dictionary<string, string> data)
    {
        var cacheKey = $"template_{templateName}";
        if (!_cache.TryGetValue(cacheKey, out string template))
        {
            template = await File.ReadAllTextAsync($"EmailTemplates/{templateName}.html");
            _cache.Set(cacheKey, template, TimeSpan.FromHours(1));
        }

        // Replace placeholders
        foreach (var kvp in data)
        {
            template = template.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }

        return template;
    }
}
```

## Future Enhancements

Potential improvements:

1. **Template Variables in Config**: Store subjects in appsettings.json
2. **Rich Template Engine**: Use Liquid, Razor, or Handlebars
3. **Template Versioning**: A/B test different template versions
4. **Template Preview**: Web UI to preview templates with sample data
5. **Dynamic Template Loading**: Load from database or CMS
6. **Template Analytics**: Track open rates, click rates

## Example: Adding a "Password Reset" Feature

Here's how to add password reset emails:

### 1. Create Template

**File**: `Infrastructure/EmailTemplates/PasswordReset.html`
```html
<!DOCTYPE html>
<html>
<body style="font-family: Arial, sans-serif;">
    <h2>Password Reset Request</h2>
    <p>Hello {{FirstName}},</p>
    <p>Click below to reset your password:</p>
    <a href="{{ResetLink}}" style="background: #4CAF50; color: white; padding: 12px 24px;">
        Reset Password
    </a>
    <p>This link expires in 1 hour.</p>
</body>
</html>
```

### 2. Add Subject

Update `EmailTemplateService.cs`:
```csharp
{ "PasswordReset", "Reset Your Gearify Password" }
```

### 3. Create Command Handler

```csharp
public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand>
{
    private readonly IEmailTemplateService _templateService;
    private readonly IEmailService _emailService;

    public async Task Handle(RequestPasswordResetCommand request, CancellationToken ct)
    {
        // Generate reset token
        var resetToken = Guid.NewGuid().ToString("N");
        var resetLink = $"https://app.gearify.com/reset-password?token={resetToken}";

        // Prepare template data
        var data = new Dictionary<string, string>
        {
            { "FirstName", user.FirstName },
            { "ResetLink", resetLink }
        };

        // Render and send
        var html = await _templateService.RenderTemplateAsync("PasswordReset", data);
        var subject = _templateService.GetSubject("PasswordReset");

        await _emailService.SendEmailAsync(user.Email, subject, html, ct);
    }
}
```

## Summary

**Advantages of Template-Based Emails**:
- ✅ Easy to edit without code changes
- ✅ Designers can work on templates independently
- ✅ Preview templates in browser before sending
- ✅ Consistent branding across all emails
- ✅ Supports A/B testing
- ✅ Can be stored in database for dynamic updates

**When to Use**:
- Transactional emails (welcome, verification, reset password)
- Notification emails
- Marketing emails
- Reports and summaries

**Current Templates**:
1. `WelcomeEmail.html` - User registration welcome email
2. `PasswordReset.html` - Password reset email (example)

Add more templates as needed for your application!
