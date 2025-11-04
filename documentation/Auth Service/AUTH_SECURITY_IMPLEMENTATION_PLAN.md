# Auth Service Security Features - Implementation Plan

## Overview

Implementing comprehensive enterprise-grade security features for the Gearify auth service.

## Features to Implement

### 1. Multi-Factor Authentication (MFA)
- ✅ TOTP (Authenticator apps like Google Authenticator, Authy)
- ✅ Email-based OTP (6-digit code)
- ✅ SMS-based OTP (6-digit code via AWS SNS)
- ✅ Backup codes (10 single-use codes)
- ✅ Enable/Disable MFA
- ✅ MFA verification during login

### 2. Password Reset Flow
- ✅ Request password reset (sends email)
- ✅ Validate reset token
- ✅ Set new password
- ✅ Invalidate all sessions after reset
- ✅ Email notification

### 3. Account Lockout
- ✅ Track failed login attempts
- ✅ Lock account after 5 failed attempts
- ✅ Automatic unlock after 30 minutes
- ✅ Manual unlock by admin
- ✅ Email notification on lockout

### 4. Password Policy
- ✅ Minimum 8 characters
- ✅ At least 1 uppercase letter
- ✅ At least 1 lowercase letter
- ✅ At least 1 number
- ✅ At least 1 special character
- ✅ Password history (prevent last 5 passwords)
- ✅ Clear validation messages

### 5. Session Management
- ✅ Track active sessions per user
- ✅ Store session metadata (device, IP, location)
- ✅ Revoke specific session
- ✅ Logout from all devices
- ✅ Session timeout configuration

## Database Schema Changes

### User Entity Updates
```csharp
// MFA fields
public bool MfaEnabled { get; set; }
public MfaMethod PreferredMfaMethod { get; set; }
public string? TotpSecret { get; set; }
public string[]? BackupCodes { get; set; }
public DateTime? LastMfaSetupAt { get; set; }

// Password reset fields
public string? PasswordResetToken { get; set; }
public DateTime? PasswordResetTokenExpiry { get; set; }

// Account lockout fields
public int FailedLoginAttempts { get; set; }
public DateTime? LockoutEnd { get; set; }
public bool LockoutEnabled { get; set; }

// Password history
public string[]? PasswordHistory { get; set; } // Last 5 hashed passwords

// Session tracking
public int ActiveSessionCount { get; set; }
```

### New Entity: UserSession
```csharp
public class UserSession
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public string RefreshToken { get; set; }
    public string DeviceInfo { get; set; }
    public string IpAddress { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
```

### New Entity: MfaCode
```csharp
public class MfaCode
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Code { get; set; }
    public MfaMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public int AttemptCount { get; set; }
}
```

## Enums

```csharp
public enum MfaMethod
{
    None = 0,
    Totp = 1,      // Authenticator app
    Email = 2,     // Email OTP
    Sms = 3        // SMS OTP
}
```

## API Endpoints

### MFA Endpoints

**POST /api/auth/mfa/setup/totp**
- Generates TOTP secret
- Returns QR code (base64 image)
- Returns manual entry key

**POST /api/auth/mfa/setup/email**
- Sets email as MFA method
- Sends test code

**POST /api/auth/mfa/setup/sms**
- Sets phone as MFA method
- Sends test code

**POST /api/auth/mfa/verify-setup**
- Verifies MFA setup with code
- Generates backup codes
- Enables MFA

**POST /api/auth/mfa/disable**
- Disables MFA (requires password + MFA code)

**POST /api/auth/mfa/regenerate-backup-codes**
- Generates new backup codes
- Invalidates old ones

**POST /api/auth/mfa/verify**
- Verifies MFA code during login
- Returns JWT on success

### Password Reset Endpoints

**POST /api/auth/password/forgot**
- Request: `{ "email": "..." }`
- Sends reset email
- Returns: Success message

**POST /api/auth/password/reset**
- Request: `{ "token": "...", "newPassword": "..." }`
- Validates token
- Updates password
- Returns: Success message

**POST /api/auth/password/change**
- Requires authentication
- Request: `{ "currentPassword": "...", "newPassword": "..." }`
- Validates current password
- Updates password

### Session Management Endpoints

**GET /api/auth/sessions**
- Lists all active sessions for user
- Returns session details

**DELETE /api/auth/sessions/{sessionId}**
- Revokes specific session

**DELETE /api/auth/sessions/all**
- Logs out from all devices
- Keeps current session active

### Account Management Endpoints

**POST /api/auth/account/unlock**
- Admin only
- Unlocks locked account

**GET /api/auth/account/lockout-status**
- Returns lockout information

## Configuration

### appsettings.json
```json
{
  "Security": {
    "PasswordPolicy": {
      "MinimumLength": 8,
      "RequireUppercase": true,
      "RequireLowercase": true,
      "RequireDigit": true,
      "RequireSpecialChar": true,
      "PasswordHistoryCount": 5
    },
    "AccountLockout": {
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 30,
      "AllowedAttemptsBeforeLockout": 5
    },
    "Mfa": {
      "CodeExpiryMinutes": 5,
      "MaxVerificationAttempts": 3,
      "BackupCodesCount": 10,
      "TotpIssuer": "Gearify"
    },
    "PasswordReset": {
      "TokenExpiryHours": 1,
      "AllowedResetAttemptsPerDay": 3
    },
    "Session": {
      "MaxConcurrentSessions": 5,
      "SessionTimeoutMinutes": 60,
      "RefreshTokenExpiryDays": 7
    }
  },
  "Sms": {
    "Provider": "AWS_SNS",
    "FromNumber": "+1234567890"
  }
}
```

## NuGet Packages Needed

```xml
<PackageReference Include="OtpNet" Version="1.9.2" />  <!-- TOTP generation -->
<PackageReference Include="QRCoder" Version="1.4.3" />  <!-- QR code generation -->
<PackageReference Include="AWSSDK.SimpleNotificationService" Version="3.7.0" />  <!-- SMS via SNS -->
```

## Implementation Order

### Phase 1: Foundation (Day 1)
1. ✅ Update User entity with new fields
2. ✅ Create UserSession entity
3. ✅ Create MfaCode entity
4. ✅ Install NuGet packages
5. ✅ Create configuration models
6. ✅ Update DynamoDB table schemas

### Phase 2: Password Policy & Lockout (Day 1)
1. ✅ Create password policy validator
2. ✅ Implement password history tracking
3. ✅ Implement account lockout logic
4. ✅ Update login handler to check lockout
5. ✅ Create lockout email template

### Phase 3: Password Reset (Day 2)
1. ✅ Create ForgotPasswordCommand
2. ✅ Create ResetPasswordCommand
3. ✅ Create ChangePasswordCommand
4. ✅ Create email templates (forgot, reset success)
5. ✅ Create API endpoints

### Phase 4: MFA - TOTP (Day 2-3)
1. ✅ Create TOTP service
2. ✅ Create SetupTotpCommand
3. ✅ Create VerifyMfaCommand
4. ✅ Generate QR codes
5. ✅ Generate backup codes
6. ✅ Update login flow for MFA

### Phase 5: MFA - Email & SMS (Day 3)
1. ✅ Create OTP generation service
2. ✅ Create email OTP sender
3. ✅ Create SMS service (AWS SNS)
4. ✅ Create SetupEmailMfaCommand
5. ✅ Create SetupSmsMfaCommand
6. ✅ Create email templates for OTP

### Phase 6: Session Management (Day 4)
1. ✅ Create session tracking in login
2. ✅ Create UserSessionRepository
3. ✅ Create session management commands
4. ✅ Create API endpoints
5. ✅ Implement session revocation

### Phase 7: Testing & Documentation (Day 5)
1. ✅ Test all flows
2. ✅ Create API documentation
3. ✅ Create user guides
4. ✅ Update Swagger documentation

## Email Templates Needed

1. **AccountLocked.html/txt** - Sent when account is locked
2. **PasswordResetRequest.html/txt** - Sent when password reset requested
3. **PasswordResetSuccess.html/txt** - Sent after successful password reset
4. **PasswordChanged.html/txt** - Sent when password changed (authenticated)
5. **MfaEnabled.html/txt** - Sent when MFA is enabled
6. **MfaDisabled.html/txt** - Sent when MFA is disabled
7. **MfaCode.html/txt** - Sent for email-based MFA codes
8. **BackupCodesGenerated.html/txt** - Sent with backup codes
9. **SuspiciousLogin.html/txt** - Sent for suspicious activity
10. **NewDeviceLogin.html/txt** - Sent when login from new device

## Security Considerations

### Password Reset
- Generate cryptographically secure tokens
- Short expiry (1 hour)
- Rate limit requests (3 per day)
- Invalidate token after use
- Send notification to user email

### MFA
- TOTP uses standard algorithm (RFC 6238)
- Codes expire after 5 minutes
- Maximum 3 verification attempts
- Backup codes are single-use
- Store codes hashed

### Account Lockout
- Progressive lockout (longer with each lockout)
- IP-based tracking option
- Admin override capability
- Email notification

### Session Management
- Store minimal session data
- Automatic cleanup of expired sessions
- Secure session token generation
- HttpOnly, Secure cookie flags

## Testing Plan

### Unit Tests
- Password policy validation
- Password hashing and verification
- TOTP generation and validation
- OTP generation and validation
- Token generation and validation
- Lockout logic

### Integration Tests
- Full login flow with MFA
- Password reset flow
- Session management
- Account lockout and unlock

### Manual Testing
- Test with different MFA methods
- Test password reset email delivery
- Test account lockout notifications
- Test session revocation

## Monitoring & Metrics

Track:
- Failed login attempts per user
- MFA enrollment rate
- Password reset requests
- Account lockout frequency
- Session duration average
- Concurrent sessions per user

## Compliance

### GDPR
- User can export their data
- User can delete their account
- Audit trail of security events
- Data retention policies

### OWASP Top 10
- ✅ Broken Access Control - Addressed via session management
- ✅ Cryptographic Failures - Addressed via BCrypt, secure tokens
- ✅ Injection - Using parameterized queries
- ✅ Insecure Design - Security by design
- ✅ Security Misconfiguration - Configuration validation
- ✅ Identification and Authentication Failures - MFA, lockout, password policy

## Next Steps

Start with Phase 1 - I'll begin implementing the foundation (entities, enums, configuration) now.

Ready to proceed?
