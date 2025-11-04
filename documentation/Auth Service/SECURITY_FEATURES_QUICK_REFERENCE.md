# Security Features - Quick Reference Guide

## 🚀 Quick Start

### Prerequisites
- LocalStack running with DynamoDB tables created
- Auth service configured with Security settings in appsettings.json
- AWS SES configured for emails
- AWS SNS configured for SMS (optional)

### Run the Service
```bash
cd C:\Gearify\gearify-auth-svc
dotnet run --launch-profile "Local Debug"
```

Access Swagger UI: `http://localhost:5000/swagger`

---

## 📋 Feature Cheat Sheet

| Feature | Endpoints | Auth Required | Key Config |
|---------|-----------|---------------|------------|
| Password Policy | Registration, Password Change | No / Yes | `Security:PasswordPolicy` |
| Account Lockout | Login | No | `Security:AccountLockout` |
| Password Reset | /forgot, /reset | No | `Security:PasswordReset` |
| Change Password | /change | Yes | `Security:PasswordPolicy` |
| TOTP MFA | /setup/totp, /verify, /disable | Yes | `Security:Mfa` |
| Email OTP | Login flow | No | `Security:Mfa` |
| SMS OTP | Login flow | No | `Security:Mfa` + `Sms` |
| Sessions | /active, /revoke | Yes | `Security:Session` |

---

## 🔑 API Endpoints Quick Reference

### Authentication (Existing)
```
POST   /api/auth/register          Register new user
POST   /api/auth/login             Login
POST   /api/auth/refresh           Refresh token
POST   /api/auth/verify-email      Verify email
```

### Password Management (NEW)
```
POST   /api/password/forgot        Request password reset
POST   /api/password/reset         Reset password with token
POST   /api/password/change        Change password (authenticated)
```

### MFA Management (NEW)
```
POST   /api/mfa/setup/totp         Setup authenticator app MFA
POST   /api/mfa/verify             Verify and enable MFA
POST   /api/mfa/disable            Disable MFA
```

### Session Management (NEW)
```
GET    /api/session/active         Get active sessions
POST   /api/session/revoke/{id}    Revoke specific session
POST   /api/session/revoke-all     Revoke all sessions
```

---

## 🔧 Configuration Defaults

### Password Policy
```json
{
  "MinimumLength": 8,
  "RequireUppercase": true,
  "RequireLowercase": true,
  "RequireDigit": true,
  "RequireSpecialChar": true,
  "PasswordHistoryCount": 5
}
```

**Example Valid Password**: `SecurePass123!`

### Account Lockout
```json
{
  "MaxFailedAttempts": 5,
  "LockoutDurationMinutes": 30,
  "EnableLockout": true
}
```

**Behavior**: After 5 failed login attempts, account is locked for 30 minutes.

### MFA Settings
```json
{
  "CodeExpiryMinutes": 5,
  "MaxVerificationAttempts": 3,
  "BackupCodesCount": 10,
  "TotpIssuer": "Gearify"
}
```

**Code Format**: 6-digit numeric (e.g., `123456`)
**Backup Code Format**: `ABCD-1234` (8 alphanumeric with dash)

### Session Settings
```json
{
  "MaxConcurrentSessions": 5,
  "SessionTimeoutMinutes": 60,
  "RefreshTokenExpiryDays": 7
}
```

**Behavior**: User can have max 5 active sessions. Oldest is removed when limit exceeded.

---

## 📧 Email Templates

All templates support both HTML and plain text:

| Template | Trigger | Placeholders |
|----------|---------|--------------|
| AccountLocked | Account locked after failed attempts | FirstName, FailedAttempts, LockoutTime, UnlockTime |
| AccountUnlocked | Account unlocked (auto or manual) | FirstName, UnlockTime, UnlockMethod |
| PasswordResetRequest | User requests password reset | FirstName, ResetLink, ExpiryHours |
| PasswordResetSuccess | Password reset completed | FirstName, Email, ChangeTime |
| PasswordChanged | Password changed while logged in | FirstName, Email, ChangeTime |
| MfaEnabled | MFA enabled successfully | FirstName, Method, SetupTime |
| MfaDisabled | MFA disabled | FirstName, DisabledTime |

---

## 🧪 Testing Scenarios

### Test 1: Password Policy Enforcement
```bash
# Try registering with weak password
POST /api/auth/register
{
  "email": "test@example.com",
  "password": "weak",  # Should fail
  "firstName": "Test",
  "lastName": "User"
}

# Expected: 400 Bad Request
# Message: "Password must be at least 8 characters long. Password must contain..."
```

### Test 2: Account Lockout
```bash
# Login with wrong password 5 times
for i in {1..5}; do
  curl -X POST http://localhost:5000/api/auth/login \
    -H "Content-Type: application/json" \
    -H "X-Tenant-Id: default-tenant" \
    -d '{"email":"user@example.com","password":"wrong"}'
done

# 6th attempt should return:
# "Account is locked. Please try again in 30 minutes."
```

### Test 3: TOTP MFA Setup
```bash
# Step 1: Setup TOTP
POST /api/mfa/setup/totp
Authorization: Bearer {your-token}

# Response includes QR code and backup codes
# Scan QR code with Google Authenticator

# Step 2: Verify with code from app
POST /api/mfa/verify
Authorization: Bearer {your-token}
{
  "code": "123456"  # From authenticator app
}

# Response: "MFA has been enabled successfully."
```

### Test 4: Password Reset Flow
```bash
# Step 1: Request reset
POST /api/password/forgot
{
  "email": "user@example.com"
}

# Check email for reset link

# Step 2: Reset password
POST /api/password/reset
{
  "email": "user@example.com",
  "resetToken": "{token-from-email}",
  "newPassword": "NewSecure@Pass123"
}

# Response: "Your password has been reset successfully."
```

### Test 5: Session Management
```bash
# Login from multiple devices/browsers
# Each login creates a new session

# View all sessions
GET /api/session/active
Authorization: Bearer {your-token}

# Revoke a specific session
POST /api/session/revoke/sess-456
Authorization: Bearer {your-token}

# Logout from all devices
POST /api/session/revoke-all
Authorization: Bearer {your-token}
```

---

## ⚠️ Common Issues & Solutions

### Issue: Account Locked Email Not Received
**Solution**:
- Check AWS SES configuration
- Verify email is verified in SES
- Check LocalStack logs for SES calls
- Ensure `Email:FromEmail` in appsettings.json is correct

### Issue: MFA QR Code Not Scanning
**Solution**:
- Ensure QR code is properly Base64 decoded
- Try manual entry key instead
- Verify TOTP settings (digits=6, period=30)

### Issue: Password Rejected Despite Meeting Requirements
**Solution**:
- Check if password was used recently (history check)
- Verify all requirements: uppercase, lowercase, digit, special char
- Ensure minimum length is met

### Issue: Session Not Revoked
**Solution**:
- Verify session ID is correct
- Check if user has permission to revoke that session
- Ensure session hasn't already expired

### Issue: SMS OTP Not Received
**Solution**:
- Verify AWS SNS is configured
- Check phone number is in E.164 format (+1234567890)
- For LocalStack, check SNS mock logs
- Verify `Sms:FromNumber` in appsettings.json

---

## 🔒 Security Best Practices

### For Developers

1. **Never Log Sensitive Data**
   - ❌ Don't log passwords, tokens, or codes
   - ✅ Log user IDs and event types

2. **Always Use HTTPS in Production**
   - Tokens and passwords must be encrypted in transit

3. **Validate Input**
   - Use FluentValidation for all API inputs
   - Sanitize email addresses (lowercase, trim)

4. **Rate Limiting**
   - Consider adding rate limiting to password reset
   - Implement API rate limiting (not included in this version)

5. **Token Expiry**
   - Keep token expiry times short
   - Implement token refresh mechanism

### For Users

1. **Use Authenticator Apps** (Most Secure)
   - Google Authenticator
   - Microsoft Authenticator
   - Authy

2. **Store Backup Codes Safely**
   - Print and store in secure location
   - Never share backup codes

3. **Use Unique Passwords**
   - Don't reuse passwords across sites
   - Use password manager

4. **Enable MFA**
   - Adds extra layer of security
   - Protects against password compromise

5. **Review Active Sessions Regularly**
   - Revoke unknown sessions
   - Logout from unused devices

---

## 📊 Database Tables Summary

### UserSessions
```
Purpose: Track user sessions
PK: USER#{userId}
SK: SESSION#{sessionId}
Retention: Cleaned up automatically on expiry
```

### MfaCodes
```
Purpose: Store OTP codes temporarily
PK: USER#{userId}
SK: MFACODE#{codeId}
Retention: 5 minutes (auto-cleanup)
```

### Users (Updated)
```
Purpose: User authentication and profile
New Fields: 15+ security-related fields
- MFA settings (TotpSecret, BackupCodes)
- Lockout tracking (FailedLoginAttempts, LockoutEnd)
- Password history
- Password reset tokens
```

---

## 🎯 Feature Flags (Future Enhancement)

Not currently implemented, but recommended additions:

```json
{
  "Features": {
    "EnablePasswordPolicy": true,
    "EnableAccountLockout": true,
    "EnableMfa": true,
    "EnableSessionManagement": true,
    "EnablePasswordHistory": true,
    "MfaMethods": {
      "Totp": true,
      "Email": true,
      "Sms": false  // Disable if no SMS provider
    }
  }
}
```

---

## 📞 Support & Resources

### Documentation
- **Implementation Summary**: `SECURITY_FEATURES_IMPLEMENTATION_SUMMARY.md`
- **Detailed Docs**: `SECURITY_FEATURES_DETAILED_DOCUMENTATION.md`
- **This Guide**: `SECURITY_FEATURES_QUICK_REFERENCE.md`

### Standards & References
- **TOTP**: RFC 6238
- **BCrypt**: Adaptive hashing function
- **JWT**: RFC 7519
- **OWASP**: Authentication guidelines
- **NIST**: Password guidelines (SP 800-63B)

### Key Libraries
- **OtpNet**: TOTP implementation
- **QRCoder**: QR code generation
- **BCrypt.Net**: Password hashing
- **AWS SDK**: SES (email), SNS (SMS)

---

## ✅ Pre-Production Checklist

### Configuration
- [ ] Update JWT secret for production
- [ ] Configure production SES domain
- [ ] Set up SMS sender number
- [ ] Review all security settings
- [ ] Update CORS allowed origins
- [ ] Configure proper rate limits

### Testing
- [ ] Test password policy enforcement
- [ ] Test account lockout and unlock
- [ ] Test password reset flow
- [ ] Test TOTP MFA setup and login
- [ ] Test session management
- [ ] Test email delivery
- [ ] Test SMS delivery (if enabled)

### Infrastructure
- [ ] Create DynamoDB tables in production
- [ ] Set up DynamoDB backup/restore
- [ ] Configure CloudWatch alarms
- [ ] Set up logging and monitoring
- [ ] Verify AWS SES sending limits
- [ ] Verify AWS SNS quotas

### Security
- [ ] Review all email templates
- [ ] Verify HTTPS is enforced
- [ ] Audit all endpoints for authorization
- [ ] Review token expiry times
- [ ] Test password strength requirements
- [ ] Verify session timeout behavior

---

## 🎓 Training Resources

### For Administrators
1. **Account Management**
   - How to manually unlock accounts
   - Reviewing security events
   - Managing MFA for users

2. **Monitoring**
   - Tracking failed login attempts
   - Monitoring lockout events
   - Session analytics

### For End Users
1. **Getting Started**
   - Setting up MFA
   - Using backup codes
   - Managing active sessions

2. **Security**
   - Creating strong passwords
   - Recognizing phishing attempts
   - Protecting account credentials

---

**Document Version**: 1.0
**Last Updated**: October 26, 2025
**Maintained By**: Gearify Development Team
