# Email Verification Testing Guide

## Overview

This guide explains how the email verification system works and how to test it. After implementing email verification, users must verify their email address before they can access the application.

## Table of Contents

- [How It Works](#how-it-works)
- [Architecture](#architecture)
- [Testing the Email Verification Flow](#testing-the-email-verification-flow)
- [Troubleshooting](#troubleshooting)
- [Configuration](#configuration)

---

## How It Works

### Registration Flow

1. **User registers** with email and password
2. **Backend generates** a verification token (valid for 24 hours)
3. **User is NOT auto-logged in** (tokens are generated but not returned to frontend)
4. **Verification email** is sent via LocalStack SES → MailHog (in development)
5. **User sees** "Check your email" message
6. **User clicks** verification link in email
7. **Email is verified** in the database
8. **User can now login** with their verified account

### Login Flow with Verification

- **Unverified users** can register but cannot login
- **Verified users** can login normally
- Email verification status is stored in the `EmailVerified` field in the User entity

---

## Architecture

### Backend Components

#### 1. **User Registration** (`RegisterUserCommandHandler.cs`)
```csharp
// Generates email verification token
var emailVerificationToken = Guid.NewGuid().ToString("N");
var emailVerificationExpiry = DateTime.UtcNow.AddHours(24);

// Creates user with EmailVerified = false
user.EmailVerified = false;
user.EmailVerificationToken = emailVerificationToken;
user.EmailVerificationTokenExpiry = emailVerificationExpiry;
```

#### 2. **Email Sending** (`SendWelcomeEmailHandler.cs`)
- Triggered by `UserCreatedEvent`
- Sends email using `SesEmailService`
- Email template: `WelcomeEmail.html`
- Verification link: `http://localhost:4200/auth/verify-email?token={token}`

#### 3. **Email Verification** (`VerifyEmailCommandHandler.cs`)
```csharp
// Verifies token and marks email as verified
user.EmailVerified = true;
user.EmailVerificationToken = null;
user.EmailVerificationTokenExpiry = null;
```

#### 4. **API Endpoint** (`AuthController.cs`)
```csharp
[HttpPost("verify-email")]
public async Task<IActionResult> VerifyEmail([FromQuery] string token)
```

### Frontend Components

#### 1. **Registration Component** (`register.component.ts`)
- Shows "Verify Your Email" message after registration
- Does NOT auto-login users
- Does NOT store tokens

#### 2. **Verify Email Component** (`verify-email.component.ts`)
- Handles verification link from email
- Calls `/api/auth/verify-email?token={token}`
- Shows success/error states
- Auto-redirects to login after 3 seconds

#### 3. **Auth Service** (`auth.service.ts`)
```typescript
// Does not store tokens on registration
register(data: RegisterRequest): Observable<...> {
  // No token storage - user must verify email first
}

// Verifies email with token
verifyEmail(token: string): Observable<{ message: string }> {
  return this.api.post(`${API_CONFIG.ENDPOINTS.VERIFY_EMAIL}?token=${token}`, {});
}
```

---

## Testing the Email Verification Flow

### Prerequisites

1. **LocalStack** running with SES configured
2. **MailHog** running on port 8025
3. **Frontend** running on port 4200
4. **Auth Service** running on port 5011

### Step-by-Step Testing

#### Step 1: Open Required Pages

```bash
# Open Registration Page
start http://localhost:4200/auth/register

# Open MailHog (Email Viewer)
start http://localhost:8025
```

#### Step 2: Register a New User

Fill in the registration form:
```
First Name:     Test
Last Name:      User
Email:          testuser@example.com
Password:       Test1234!
Confirm:        Test1234!
```

Click **"Create Account"**

#### Step 3: Verify Success Message

You should see:
- ✅ Email icon (envelope SVG)
- ✅ "Verify Your Email" heading
- ✅ Message showing your email address
- ✅ Instructions to check inbox
- ✅ "Go to Login" button
- ❌ NOT redirected to home
- ❌ NOT logged in automatically

#### Step 4: Check MailHog for Email

1. Switch to MailHog tab (http://localhost:8025)
2. Look for new email:
   - **From**: Gearify <noreply@gearify.com>
   - **To**: testuser@example.com
   - **Subject**: Welcome to Gearify
3. Click on the email to view it

#### Step 5: View Email Content

The email should contain:
- Purple gradient header
- "Welcome to Gearify!" title
- Personalized greeting: "Hello Test! 👋"
- "Verify Email Address" button (purple)
- Alternative link for copy/paste
- Expiration warning (24 hours)

#### Step 6: Click Verification Link

1. Click the "Verify Email Address" button
2. New tab opens showing:
   - Loading spinner (briefly)
   - "Email Verified!" message with green checkmark
   - "Redirecting to login..." text
3. Auto-redirect to login page after 3 seconds

#### Step 7: Login with Verified Account

On login page:
```
Email:    testuser@example.com
Password: Test1234!
```

Click **"Sign In"**

✅ **Success!** You are now logged in.

---

## Troubleshooting

### Email Not Appearing in MailHog

#### Check 1: Verify LocalStack Configuration

```bash
# Check if SMTP_HOST is set
docker exec gearify-localstack env | grep SMTP

# Should show:
# SMTP_HOST=mailhog:1025
# SES_SMTP_HOST=mailhog:1025
```

#### Check 2: Check LocalStack Logs

```bash
docker-compose logs --tail=50 localstack | grep -i "ses\|email\|smtp"
```

Look for:
- ✅ `AWS ses.SendEmail => 200` (email was sent)
- ✅ `Sending email to {email}` (email forwarding started)
- ❌ `STARTTLS extension not supported` (configuration issue)

#### Check 3: Verify MailHog is Running

```bash
docker-compose ps mailhog
```

Should show:
```
NAME              STATUS          PORTS
gearify-mailhog   Up X minutes    0.0.0.0:1025->1025/tcp, 0.0.0.0:8025->8025/tcp
```

#### Check 4: Test Connectivity

```bash
# Test if LocalStack can reach MailHog
docker exec gearify-localstack ping -c 2 mailhog
```

#### Fix: Restart LocalStack

If SMTP configuration is missing:

1. Edit `docker-compose.yml` and add:
```yaml
environment:
  - SMTP_HOST=mailhog:1025
  - SES_SMTP_HOST=mailhog:1025
```

2. Restart:
```bash
docker-compose restart localstack
```

3. Wait for healthy status:
```bash
docker-compose ps localstack
# STATUS should show (healthy)
```

### Verification Link Not Working

#### Invalid or Expired Token

**Symptoms**:
- "Invalid or expired verification token" error
- "Verification token has expired" error

**Causes**:
- Token is older than 24 hours
- Token was already used
- Token doesn't exist in database

**Solution**:
- Register again to get a new verification email
- Check token expiry in database:
```bash
aws dynamodb scan \
  --table-name gearify-users \
  --endpoint-url http://localhost:4566 \
  --filter-expression "Email = :email" \
  --expression-attribute-values '{":email":{"S":"testuser@example.com"}}' \
  --region us-east-1
```

#### Wrong URL Format

**Expected**: `http://localhost:4200/auth/verify-email?token={token}`

**Check backend configuration**:
```csharp
// SesEmailService.cs
var verificationLink = $"{_webAppUrl}/auth/verify-email?token={verificationToken}";
```

### Already Verified

If you get "Email already verified", the account is ready to use. Just login normally.

---

## Configuration

### Backend Configuration

#### appsettings.Development.json

```json
{
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify"
  },
  "WebAppUrl": "http://localhost:4200"
}
```

#### Email Template

Location: `gearify-auth-svc/Infrastructure/EmailTemplates/WelcomeEmail.html`

Template variables:
- `{{FirstName}}` - User's first name
- `{{VerificationLink}}` - Full verification URL

### Frontend Configuration

#### API Endpoint

`src/app/shared/constants/api.constants.ts`:
```typescript
VERIFY_EMAIL: '/api/auth/verify-email'
```

#### Route Configuration

`src/app/features/auth/auth.routes.ts`:
```typescript
{
  path: 'verify-email',
  loadComponent: () => import('./verify-email.component').then(m => m.VerifyEmailComponent)
}
```

### Docker Configuration

#### docker-compose.yml

```yaml
localstack:
  environment:
    - SMTP_HOST=mailhog:1025
    - SES_SMTP_HOST=mailhog:1025
```

---

## Database Schema

### User Entity Fields

```csharp
public bool EmailVerified { get; set; } = false;
public string? EmailVerificationToken { get; set; }
public DateTime? EmailVerificationTokenExpiry { get; set; }
```

### DynamoDB Attributes

```
EmailVerified: BOOL
EmailVerificationToken: S (String)
EmailVerificationTokenExpiry: S (ISO 8601 DateTime)
```

---

## Security Considerations

1. **Token Expiry**: Tokens expire after 24 hours
2. **Single Use**: Tokens are cleared after successful verification
3. **Secure Generation**: Uses `Guid.NewGuid().ToString("N")` (32 hex characters)
4. **No Auto-Login**: Prevents account takeover if registration email is compromised
5. **HTTPS Required**: In production, use HTTPS for verification links

---

## Production Deployment

### Changes Needed for Production

1. **Use Real Email Service**:
   - Configure AWS SES in production
   - Remove MailHog (development only)
   - Update `SMTP_HOST` or use SES directly

2. **Update WebAppUrl**:
```json
{
  "WebAppUrl": "https://yourdomain.com"
}
```

3. **Email Verification Enforcement**:
   - Add middleware to check `EmailVerified` on protected routes
   - Return 403 Forbidden if email not verified

4. **Monitoring**:
   - Track email delivery rates
   - Monitor verification completion rates
   - Alert on high bounce rates

---

## Best Practices

1. **Test with Multiple Email Addresses**: Test with various email formats
2. **Test Token Expiry**: Wait 24+ hours to test expiration
3. **Test Error Cases**: Try invalid tokens, expired tokens, already verified
4. **Clear Browser Data**: Test in incognito/private mode
5. **Check All Email Clients**: Test email rendering in different clients

---

## Related Documentation

- [AUTH_SERVICE_COMPLETE_DOCUMENTATION.md](./GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md)
- [MFA_TESTING_GUIDE.md](./MFA_TESTING_GUIDE.md)
- [LOCALSTACK_CONFIGURATION.md](../LOCALSTACK_CONFIGURATION.md)

---

## Support

If you encounter issues:
1. Check this troubleshooting guide first
2. Review LocalStack and MailHog logs
3. Verify all services are running and healthy
4. Test with a fresh user registration

---

**Last Updated**: December 2025
**Version**: 1.0
