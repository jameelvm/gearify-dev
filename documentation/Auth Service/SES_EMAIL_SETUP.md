# AWS SES Email Service Setup

This document explains how the email service works using AWS SES with LocalStack for local development and real AWS SES for production.

## Overview

The Gearify auth service now uses **AWS SES (Simple Email Service)** for all email operations:
- ✅ **Local Development**: Uses LocalStack SES (no actual emails sent)
- ✅ **Production**: Uses real AWS SES (actual emails sent)
- ✅ **Same Code**: Single implementation works for both environments

## Architecture

### Email Flow

1. **User Registration**:
   - User registers → Verification token generated
   - `UserCreatedEvent` published
   - `SendWelcomeEmailHandler` triggered
   - Welcome email sent via SES

2. **Email Verification**:
   - User clicks link in email
   - Frontend calls `/api/auth/verify-email?token={token}`
   - Email marked as verified

### Components

- **SesEmailService** (`Infrastructure/Services/SesEmailService.cs`): AWS SES implementation
- **SendWelcomeEmailHandler** (`Application/EventHandlers/SendWelcomeEmailHandler.cs`): Event handler
- **VerifyEmailCommand** (`Application/Commands/VerifyEmailCommand.cs`): Verification command

## Local Development with LocalStack

### Setup

LocalStack SES is already configured in your environment:

1. **Docker Compose** includes SES in LocalStack services (line 24)
2. **Initialization Script** verifies email addresses automatically
3. **AWS SDK** automatically uses LocalStack endpoint

### Testing Locally

#### 1. Start LocalStack
```bash
cd C:\Gearify\gearify-umbrella
docker compose up -d localstack
```

#### 2. Verify SES is Ready
```bash
# Check LocalStack health
curl http://localhost:4566/_localstack/health

# List verified email identities
awslocal ses list-verified-email-addresses
```

Expected output:
```json
{
    "VerifiedEmailAddresses": [
        "noreply@gearify.com",
        "test@example.com"
    ]
}
```

#### 3. Test Email Sending

**Register a new user:**
```bash
curl -X POST http://localhost:8080/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Tenant-ID: tenant-1" \
  -d '{
    "email": "testuser@example.com",
    "password": "Test@1234",
    "firstName": "Test",
    "lastName": "User"
  }'
```

**Check if email was sent:**
```bash
# Get SES statistics
awslocal ses get-send-statistics

# Get SES quota
awslocal ses get-send-quota
```

#### 4. View Sent Emails in LocalStack

LocalStack stores sent emails in memory. To view them:

```bash
# Get all sent emails
awslocal ses list-identities
```

**Note**: LocalStack SES doesn't have a built-in email viewer like MailHog. Emails are stored but not displayed. This is why we removed MailHog - in production, you won't see the emails either; they just get sent.

### Verifying Email Functionality

Since LocalStack doesn't show email content, verify functionality by:

1. **Check Logs**: Look for "Email sent successfully" in auth service logs
2. **Check Database**: Verify `EmailVerificationToken` is stored in user record
3. **Test Verification**: Manually call the verification endpoint with the token

```bash
# Get the token from database or logs, then:
curl -X POST "http://localhost:8080/api/auth/verify-email?token=YOUR_TOKEN_HERE"
```

## Production Setup

### Prerequisites

1. **AWS Account** with SES access
2. **Verified Email/Domain** in AWS SES
3. **IAM Credentials** with SES send permissions

### AWS SES Configuration

#### 1. Verify Email Address (Sandbox Mode)
```bash
aws ses verify-email-identity --email-address noreply@gearify.com
```

#### 2. Verify Domain (Production)
```bash
aws ses verify-domain-identity --domain gearify.com
```

Follow AWS instructions to add DNS records for domain verification.

#### 3. Request Production Access

AWS SES starts in **Sandbox Mode**:
- Can only send to verified addresses
- Limited to 200 emails/day

To send to any email address:
1. Go to AWS SES Console
2. Request production access
3. Provide use case details
4. Wait for approval (usually 24 hours)

#### 4. Set IAM Permissions

Your IAM role/user needs these permissions:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    }
  ]
}
```

### Production Configuration

Update `appsettings.Production.json`:

```json
{
  "LocalStack": {
    "UseLocalStack": false
  },
  "AWS": {
    "Region": "us-east-1"
  },
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify"
  },
  "WebAppUrl": "https://yourdomain.com"
}
```

**Important**: Set `UseLocalStack: false` in production!

## Configuration Reference

### appsettings.json

```json
{
  "LocalStack": {
    "UseLocalStack": false,
    "Config": {
      "LocalStackHost": "localhost:4566"
    }
  },
  "AWS": {
    "Region": "us-east-1"
  },
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify"
  },
  "WebAppUrl": "http://localhost:4200"
}
```

### appsettings.Development.json

```json
{
  "LocalStack": {
    "UseLocalStack": true,
    "Config": {
      "LocalStackHost": "localhost:4566"
    }
  },
  "AWS": {
    "ServiceURL": "http://localhost:4566"
  },
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify"
  },
  "WebAppUrl": "http://localhost:4200"
}
```

## API Endpoints

### POST /api/auth/register
Registers a new user and sends welcome email with verification link.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response:**
```json
{
  "token": "jwt_access_token",
  "refreshToken": "jwt_refresh_token",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "emailVerified": false
  }
}
```

### POST /api/auth/verify-email?token={token}
Verifies user's email address.

**Response:**
```json
{
  "message": "Email verified successfully"
}
```

## Monitoring & Debugging

### Check SES Statistics (Production)
```bash
aws ses get-send-statistics
```

### Check SES Quota (Production)
```bash
aws ses get-send-quota
```

### View Bounce/Complaint Notifications

Set up SNS topics for:
- Bounces
- Complaints
- Delivery notifications

```bash
aws ses set-identity-notification-topic \
  --identity noreply@gearify.com \
  --notification-type Bounce \
  --sns-topic arn:aws:sns:us-east-1:123456789:ses-bounces
```

### Auth Service Logs

The service logs all email operations:

```
Email sent successfully to user@example.com via SES. MessageId: 01000192...
```

## Troubleshooting

### LocalStack Issues

**Problem**: "Email address not verified"
```bash
# Verify email manually
awslocal ses verify-email-identity --email-address noreply@gearify.com
```

**Problem**: SES service not starting
```bash
# Check LocalStack logs
docker logs gearify-localstack

# Verify SES is in services list
docker exec gearify-localstack bash -c 'echo $SERVICES'
```

### Production Issues

**Problem**: "Email not sending"
- Check IAM permissions
- Verify email/domain is verified in SES
- Check AWS region matches configuration

**Problem**: "MessageRejected: Email address is not verified"
- You're in SES Sandbox mode
- Verify recipient email or request production access

**Problem**: High bounce rate
- Verify email addresses are valid
- Set up bounce handling
- Monitor reputation dashboard

## Cost Considerations

AWS SES Pricing (as of 2024):
- **First 62,000 emails/month**: FREE (when sent from EC2)
- **After free tier**: $0.10 per 1,000 emails
- **Attachments**: $0.12 per GB

Example costs:
- 10,000 emails/month: FREE
- 100,000 emails/month: ~$3.80
- 1,000,000 emails/month: ~$100

LocalStack (local development): FREE

## Best Practices

1. **Use Templates**: Store email templates separately for easier updates
2. **Monitor Bounces**: Set up bounce and complaint handling
3. **Track Metrics**: Use CloudWatch to monitor send rates and errors
4. **Rate Limiting**: Respect SES sending limits to avoid throttling
5. **Domain Verification**: Use domain verification instead of individual emails in production
6. **DKIM/SPF**: Configure for better deliverability
7. **Suppression List**: Implement to avoid sending to bounced addresses

## Email Templates

Current templates:
- **Welcome Email**: Sent on registration with verification link (24-hour expiry)

Future templates to implement:
- Password reset
- Email change confirmation
- Account updates
- Marketing emails (if needed)

## Next Steps

1. ✅ LocalStack SES configured
2. ✅ Email service implemented
3. ✅ Welcome email with verification
4. ⏳ Request AWS SES production access
5. ⏳ Verify production domain
6. ⏳ Set up bounce/complaint handling
7. ⏳ Configure CloudWatch monitoring
8. ⏳ Implement additional email templates

## Support

For issues:
- **LocalStack**: https://docs.localstack.cloud/user-guide/aws/ses/
- **AWS SES**: https://docs.aws.amazon.com/ses/
- **Auth Service**: Check logs at http://localhost:5341 (Seq)
