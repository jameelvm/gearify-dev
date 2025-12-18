# Authentication Testing - Quick Start Guide

## Overview

Quick reference guide for testing authentication features in the Gearify Auth Service.

---

## Available Features

| Feature | Status | Documentation |
|---------|--------|---------------|
| Email Verification | ✅ Implemented | [EMAIL_VERIFICATION_GUIDE.md](./EMAIL_VERIFICATION_GUIDE.md) |
| MFA (TOTP) | ✅ Implemented | [MFA_TESTING_GUIDE.md](./MFA_TESTING_GUIDE.md) |
| Session Management | ✅ Implemented | [GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md](./GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md) |
| Password Reset | ✅ Implemented | See main documentation |
| Account Lockout | ✅ Implemented | See security documentation |

---

## Quick Start - Email Verification

### 1. Open Pages
```bash
start http://localhost:4200/auth/register
start http://localhost:8025
```

### 2. Register
- Fill form with test data
- Click "Create Account"
- See "Verify Your Email" message

### 3. Check Email
- Switch to MailHog (http://localhost:8025)
- Click on welcome email
- Click verification link

### 4. Login
- Use verified credentials
- Access granted!

**📖 Full Guide**: [EMAIL_VERIFICATION_GUIDE.md](./EMAIL_VERIFICATION_GUIDE.md)

---

## Quick Start - MFA Testing

### 1. Prerequisites
- Install authenticator app (Google Authenticator, Authy, etc.)
- Have a registered & verified user

### 2. Open Swagger
```bash
start http://localhost:5011/swagger
```

### 3. Setup Flow
1. Login → Get token
2. Authorize Swagger with token
3. Call `POST /api/mfa/setup/totp`
4. View QR code at `C:\Gearify\view-qr-code.html`
5. Scan with authenticator app
6. Call `POST /api/mfa/verify` with 6-digit code

### 4. Test Login
- Login with email + password + MFA code
- Success!

**📖 Full Guide**: [MFA_TESTING_GUIDE.md](./MFA_TESTING_GUIDE.md)

---

## Service URLs

| Service | URL | Purpose |
|---------|-----|---------|
| Frontend | http://localhost:4200 | Angular web app |
| Auth API | http://localhost:5011 | Auth service REST API |
| Swagger | http://localhost:5011/swagger | API documentation |
| MailHog | http://localhost:8025 | Email viewer (dev) |
| LocalStack | http://localhost:4566 | AWS services (dev) |

---

## Common Issues & Quick Fixes

### Email Not in MailHog

```bash
# Check LocalStack SMTP config
docker exec gearify-localstack env | grep SMTP

# Should show:
# SMTP_HOST=mailhog:1025
# SES_SMTP_HOST=mailhog:1025

# If missing, restart LocalStack
cd gearify-umbrella
docker-compose restart localstack
```

### MFA Code Not Working

**Check phone time sync**:
- iOS: Settings → General → Date & Time → Set Automatically
- Android: Settings → System → Date & Time → Automatic

**Try next code**:
- Codes change every 30 seconds
- Wait for new code and try again

### Can't Login After Registration

**Verify email first**:
- Check MailHog for verification email
- Click verification link
- Then try login again

---

## Testing Cheatsheet

### Register User (Swagger)

```bash
POST /api/auth/register
{
  "email": "test@example.com",
  "password": "Test1234!",
  "firstName": "Test",
  "lastName": "User",
  "role": "Customer"
}
```

### Login (Swagger)

```bash
POST /api/auth/login
{
  "email": "test@example.com",
  "password": "Test1234!",
  "mfaCode": "123456"  # Optional, only if MFA enabled
}
```

### Verify Email (curl)

```bash
curl -X POST "http://localhost:5011/api/auth/verify-email?token=YOUR_TOKEN" \
  -H "X-Tenant-Id: default"
```

### Setup MFA (Swagger - Authorized)

```bash
POST /api/mfa/setup/totp
# Returns: QR code, manual key, backup codes
```

### Verify MFA (Swagger - Authorized)

```bash
POST /api/mfa/verify
{
  "code": "123456"
}
```

### Disable MFA (Swagger - Authorized)

```bash
POST /api/mfa/disable
{
  "password": "Test1234!"
}
```

---

## Test User Credentials

**Default Test User**:
```
Email:    test@test.com
Password: Test1234!
Tenant:   default
```

**After MFA Setup**:
```
Email:    test@test.com
Password: Test1234!
MFA Code: <from authenticator app>
Tenant:   default
```

---

## Utilities

### View QR Code
```bash
start C:\Gearify\view-qr-code.html
```

### Check User in Database
```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
aws dynamodb scan \
  --table-name gearify-users \
  --endpoint-url http://localhost:4566 \
  --filter-expression "Email = :email" \
  --expression-attribute-values '{":email":{"S":"test@test.com"}}' \
  --region us-east-1
```

### Delete Test User
```bash
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test \
aws dynamodb delete-item \
  --table-name gearify-users \
  --key '{"PK":{"S":"TENANT#default"},"SK":{"S":"USER#<user-id>"}}' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

---

## Development Workflow

### 1. Start Services
```bash
cd gearify-umbrella
docker-compose up -d

# Wait for services to be healthy
docker-compose ps
```

### 2. Start Frontend
```bash
cd gearify-web
npm start
```

### 3. Verify Everything Running
```bash
# Check ports
netstat -an | findstr "4200 5011 8025 4566"

# Should show all ports LISTENING
```

### 4. Test Features
- Email Verification → See guide above
- MFA → See guide above
- Session Management → Login multiple times
- Password Reset → Use forgot password flow

---

## Troubleshooting Commands

### Check All Services
```bash
# Docker services
docker-compose ps

# Frontend
netstat -an | findstr :4200

# Auth service
netstat -an | findstr :5011
```

### View Logs
```bash
# LocalStack
docker-compose logs --tail=50 localstack

# MailHog
docker-compose logs --tail=20 mailhog

# All services
docker-compose logs --tail=30
```

### Restart Services
```bash
# Restart LocalStack only
docker-compose restart localstack

# Restart all
docker-compose restart

# Stop and start fresh
docker-compose down
docker-compose up -d
```

---

## Security Testing Checklist

- [ ] Email verification required before login
- [ ] Tokens expire after 24 hours
- [ ] MFA codes change every 30 seconds
- [ ] Backup codes work only once
- [ ] Wrong MFA code fails login
- [ ] Account lockout after failed attempts
- [ ] Session expires correctly
- [ ] Refresh token works
- [ ] Logout invalidates session

---

## Performance Testing

### Load Test Registration
```bash
# Use k6, Apache Bench, or similar
# Example with curl:
for i in {1..10}; do
  curl -X POST http://localhost:5011/api/auth/register \
    -H "Content-Type: application/json" \
    -H "X-Tenant-Id: default" \
    -d "{\"email\":\"user$i@test.com\",\"password\":\"Test1234!\",\"firstName\":\"User\",\"lastName\":\"$i\"}" &
done
wait
```

### Load Test Login
```bash
# Concurrent logins
for i in {1..10}; do
  curl -X POST http://localhost:5011/api/auth/login \
    -H "Content-Type: application/json" \
    -H "X-Tenant-Id: default" \
    -d "{\"email\":\"test@test.com\",\"password\":\"Test1234!\"}" &
done
wait
```

---

## Documentation Index

### Main Guides
1. **[EMAIL_VERIFICATION_GUIDE.md](./EMAIL_VERIFICATION_GUIDE.md)**
   - Complete email verification testing guide
   - Troubleshooting email issues
   - LocalStack and MailHog configuration

2. **[MFA_TESTING_GUIDE.md](./MFA_TESTING_GUIDE.md)**
   - TOTP MFA setup and testing
   - Authenticator app configuration
   - Backup codes usage

3. **[GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md](./GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md)**
   - Complete auth service documentation
   - All API endpoints
   - Architecture overview

### Related Docs
- **Security Features**: [SECURITY_FEATURES_DETAILED_DOCUMENTATION.md](./SECURITY_FEATURES_DETAILED_DOCUMENTATION.md)
- **Architecture**: [AUTH_SECURITY_IMPLEMENTATION_PLAN.md](./AUTH_SECURITY_IMPLEMENTATION_PLAN.md)
- **LocalStack**: [../LOCALSTACK_CONFIGURATION.md](../LOCALSTACK_CONFIGURATION.md)

---

## Getting Help

### Before Asking for Help

1. Check the relevant testing guide
2. Review troubleshooting sections
3. Check service logs
4. Verify all services are running
5. Try with a fresh user account

### Useful Debug Info to Provide

```bash
# Service status
docker-compose ps

# Recent logs
docker-compose logs --tail=50

# Environment check
echo "Frontend: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:4200)"
echo "Auth API: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:5011/health)"
echo "LocalStack: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:4566/_localstack/health)"
echo "MailHog: $(curl -s -o /dev/null -w '%{http_code}' http://localhost:8025)"
```

---

## Next Steps

After testing locally:
1. ✅ Email verification works
2. ✅ MFA setup and login works
3. ✅ All services healthy
4. 📝 Document any environment-specific configuration
5. 🚀 Deploy to development environment
6. 🔒 Review security best practices
7. 📊 Set up monitoring and alerts

---

**Last Updated**: December 2025
**Version**: 1.0
**Maintained By**: Development Team
