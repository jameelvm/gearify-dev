# Multi-Factor Authentication (MFA) Testing Guide

## Overview

This guide explains how to test the Multi-Factor Authentication (MFA) system using TOTP (Time-based One-Time Password) with authenticator apps like Google Authenticator, Microsoft Authenticator, or Authy.

## Table of Contents

- [What is MFA/TOTP](#what-is-mfatotp)
- [Prerequisites](#prerequisites)
- [Complete Testing Flow](#complete-testing-flow)
- [API Endpoints](#api-endpoints)
- [Troubleshooting](#troubleshooting)
- [Security Best Practices](#security-best-practices)

---

## What is MFA/TOTP

### Multi-Factor Authentication (MFA)

MFA adds an extra layer of security by requiring users to provide two or more verification factors:
1. **Something you know** - Password
2. **Something you have** - Authenticator app generating time-based codes

### TOTP (Time-based One-Time Password)

- Generates 6-digit codes that change every 30 seconds
- Based on RFC 6238 standard
- Works offline (no internet required for code generation)
- Synchronized using device time

---

## Prerequisites

### 1. Install Authenticator App

Choose one of these free apps for your phone:

**iOS & Android:**
- **Google Authenticator** - Simple, reliable
- **Microsoft Authenticator** - Feature-rich, cloud backup
- **Authy** - Multi-device support, cloud backup
- **1Password** - If you use 1Password password manager

**Download from**:
- App Store (iOS)
- Google Play Store (Android)

### 2. Running Services

Ensure these services are running:

```bash
# Check auth service
netstat -an | findstr :5011

# Check Docker services
docker-compose ps

# Should show:
# - gearify-localstack (healthy)
# - gearify-mailhog (running)
```

### 3. Test User Account

You need a registered and verified user account:
```bash
# Register via Swagger or frontend
# Then verify email via MailHog
```

---

## Complete Testing Flow

### Step 1: Access Swagger API Documentation

```bash
# Open Swagger UI
start http://localhost:5011/swagger
```

### Step 2: Login and Get Access Token

1. **Find** `POST /api/auth/login`
2. **Click** "Try it out"
3. **Enter** credentials:
```json
{
  "email": "test@test.com",
  "password": "Test1234!"
}
```
4. **Click** "Execute"
5. **Copy** the `token` from response (starts with `eyJ...`)

### Step 3: Authorize Swagger

1. **Click** green "Authorize" button at top
2. **Paste** token in "Value" field
3. **Click** "Authorize"
4. **Click** "Close"

✅ You're now authenticated!

### Step 4: Setup MFA

1. **Find** `POST /api/mfa/setup/totp`
2. **Click** "Try it out"
3. **Click** "Execute"

**Response will include**:
```json
{
  "success": true,
  "message": "TOTP MFA setup initiated successfully.",
  "qrCodeBase64": "data:image/png;base64,iVBORw0KG...",
  "manualEntryKey": "JBSWY3DPEHPK3PXP",
  "backupCodes": [
    "12345678",
    "23456789",
    "34567890",
    "45678901",
    "56789012",
    "67890123",
    "78901234",
    "89012345",
    "90123456",
    "01234567"
  ]
}
```

**⚠️ IMPORTANT**: Save the backup codes! You'll need them if you lose access to your authenticator app.

### Step 5: Display QR Code

#### Option A: Using QR Code Viewer (Easiest)

```bash
# Open the QR viewer
start C:\Gearify\view-qr-code.html
```

1. **Copy** the entire `qrCodeBase64` value from Swagger (including `data:image/png;base64,`)
2. **Paste** in the text box
3. **Click** "Show QR Code"
4. **QR code appears** - ready to scan!

#### Option B: Manual Entry

If you can't scan the QR code:
1. Open your authenticator app
2. Choose "Enter a setup key" or "Manual entry"
3. **Account name**: Gearify (or any name you prefer)
4. **Key**: Paste the `manualEntryKey` value (e.g., `JBSWY3DPEHPK3PXP`)
5. **Type**: Time-based
6. **Algorithm**: SHA-1
7. **Digits**: 6
8. **Period**: 30 seconds

### Step 6: Scan QR Code

1. **Open** your authenticator app
2. **Tap** "+" or "Add account"
3. **Tap** "Scan QR code"
4. **Point camera** at the QR code on your screen
5. **Wait** for scan to complete

✅ Your app now shows a 6-digit code that changes every 30 seconds!

### Step 7: Verify and Enable MFA

Now verify that your setup works:

1. **Find** `POST /api/mfa/verify` in Swagger
2. **Click** "Try it out"
3. **Look at your authenticator app** - note the 6-digit code
4. **Enter** the code in request body:
```json
{
  "code": "123456"
}
```
**Replace with your actual code!**

5. **Click** "Execute"

**Success Response**:
```json
{
  "success": true,
  "message": "MFA has been enabled successfully."
}
```

**Error Response** (wrong code):
```json
{
  "success": false,
  "message": "Invalid verification code. Please try again."
}
```

**💡 Tip**: Codes change every 30 seconds. If verification fails, wait for the next code and try again.

### Step 8: Check Confirmation Email

Check MailHog (http://localhost:8025) for the "MFA Enabled" confirmation email.

### Step 9: Test MFA Login

Now test logging in with MFA:

1. **Find** `POST /api/auth/login` in Swagger
2. **Click** "Try it out"
3. **Enter** credentials WITH mfaCode:
```json
{
  "email": "test@test.com",
  "password": "Test1234!",
  "mfaCode": "654321"
}
```
**Use the CURRENT code from your app!**

4. **Click** "Execute"

**Success**: You'll receive access and refresh tokens
**Failure**: "Invalid MFA code" error

### Step 10: Test Backup Codes (Optional)

Backup codes can be used instead of TOTP codes:

1. **Login** with a backup code instead:
```json
{
  "email": "test@test.com",
  "password": "Test1234!",
  "mfaCode": "12345678"
}
```
**Use one of your backup codes!**

2. **Click** "Execute"

✅ Login successful! **Note**: Each backup code can only be used ONCE.

### Step 11: Disable MFA (Optional)

To disable MFA for testing:

1. **Find** `POST /api/mfa/disable` in Swagger
2. **Click** "Try it out"
3. **Enter** password:
```json
{
  "password": "Test1234!"
}
```
4. **Click** "Execute"

MFA is now disabled. You can set it up again anytime.

---

## API Endpoints

### Setup TOTP MFA

**Endpoint**: `POST /api/mfa/setup/totp`
**Auth**: Required (Bearer token)
**Request**: None
**Response**:
```json
{
  "success": true,
  "message": "string",
  "qrCodeBase64": "data:image/png;base64,...",
  "manualEntryKey": "string",
  "backupCodes": ["string"]
}
```

### Verify MFA Setup

**Endpoint**: `POST /api/mfa/verify`
**Auth**: Required (Bearer token)
**Request**:
```json
{
  "code": "123456"
}
```
**Response**:
```json
{
  "success": true,
  "message": "MFA has been enabled successfully."
}
```

### Login with MFA

**Endpoint**: `POST /api/auth/login`
**Auth**: Not required
**Request**:
```json
{
  "email": "user@example.com",
  "password": "password123",
  "mfaCode": "123456"
}
```
**Response**:
```json
{
  "token": "eyJ...",
  "refreshToken": "string",
  "user": { ... }
}
```

### Disable MFA

**Endpoint**: `POST /api/mfa/disable`
**Auth**: Required (Bearer token)
**Request**:
```json
{
  "password": "password123"
}
```
**Response**:
```json
{
  "success": true,
  "message": "MFA has been disabled successfully."
}
```

---

## Troubleshooting

### "Invalid verification code" Error

**Possible Causes**:

1. **Time Synchronization Issue**
   - Authenticator apps rely on accurate device time
   - **Fix**: Enable automatic time sync on your phone
     - iOS: Settings → General → Date & Time → Set Automatically
     - Android: Settings → System → Date & Time → Automatic date & time

2. **Code Expired**
   - Codes change every 30 seconds
   - **Fix**: Wait for next code and try again quickly

3. **Wrong Code**
   - Make sure you're looking at the correct account in your app
   - **Fix**: Check account name in authenticator app

4. **TOTP Not Set Up**
   - Need to run `/api/mfa/setup/totp` first
   - **Fix**: Complete setup flow before verifying

### QR Code Not Scanning

**Solutions**:

1. **Use Manual Entry**
   - Copy `manualEntryKey` from response
   - Enter manually in authenticator app

2. **Improve QR Code Display**
   - Increase browser zoom
   - Ensure good screen brightness
   - Clean phone camera lens

3. **Try Different App**
   - Some apps scan better than others
   - Try Microsoft Authenticator or Authy

### MFA Already Enabled

**Error**: "MFA has not been set up. Please initiate setup first."

**Solution**: MFA needs to be disabled first:
```bash
POST /api/mfa/disable
{
  "password": "your-password"
}
```

Then run setup again.

### Lost Authenticator App

**If you still have backup codes**:
1. Login using a backup code
2. Disable MFA
3. Set up MFA again with new device

**If you lost backup codes**:
- Contact system administrator
- Database admin can manually disable MFA by setting `MfaEnabled = false`

### Database Query to Check MFA Status

```bash
# Check user's MFA status
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test aws dynamodb scan \
  --table-name gearify-users \
  --endpoint-url http://localhost:4566 \
  --filter-expression "Email = :email" \
  --expression-attribute-values '{":email":{"S":"test@test.com"}}' \
  --region us-east-1
```

Look for:
```json
{
  "MfaEnabled": { "BOOL": true },
  "PreferredMfaMethod": { "S": "Totp" }
}
```

---

## Security Best Practices

### 1. Backup Codes

- ✅ Save backup codes in a secure location (password manager, encrypted file)
- ✅ Print and store physically in a safe place
- ❌ Don't store in plain text files
- ❌ Don't share backup codes

### 2. Authenticator App Security

- ✅ Use apps with cloud backup (Microsoft Authenticator, Authy)
- ✅ Enable biometric lock on your phone
- ✅ Keep your phone OS updated
- ❌ Don't screenshot TOTP codes
- ❌ Don't use SMS-based 2FA if TOTP is available (more secure)

### 3. Account Recovery

- ✅ Have multiple backup codes
- ✅ Set up MFA on multiple devices (if app supports it)
- ✅ Document recovery process
- ❌ Don't rely on single device only

### 4. Testing

- ✅ Test login with MFA immediately after setup
- ✅ Test backup codes work
- ✅ Test time synchronization
- ❌ Don't wait until emergency to test backup codes

### 5. Production Deployment

- ✅ Enforce MFA for admin accounts
- ✅ Log MFA setup/disable events
- ✅ Monitor failed MFA attempts
- ✅ Implement account lockout after X failed attempts
- ❌ Don't make MFA optional for sensitive operations

---

## MFA Implementation Details

### Backend Components

**Files**:
- `Application/Commands/SetupTotpMfaCommand.cs`
- `Application/Commands/SetupTotpMfaCommandHandler.cs`
- `Application/Commands/VerifyMfaSetupCommand.cs`
- `Application/Commands/VerifyMfaSetupCommandHandler.cs`
- `Application/Commands/DisableMfaCommand.cs`
- `Application/Commands/DisableMfaCommandHandler.cs`
- `Infrastructure/Services/TotpService.cs`
- `Infrastructure/Services/OtpService.cs`
- `API/Controllers/MfaController.cs`

**Database Fields** (User entity):
```csharp
public bool MfaEnabled { get; set; } = false;
public string? PreferredMfaMethod { get; set; }
public string? TotpSecret { get; set; }
public List<string> BackupCodes { get; set; } = new();
public DateTime? LastMfaSetupAt { get; set; }
```

**TOTP Algorithm**:
- RFC 6238 compliant
- SHA-1 hash algorithm
- 30-second time step
- 6-digit codes
- Uses `OtpNet` library

---

## Testing Checklist

- [ ] Install authenticator app on phone
- [ ] Create test user account
- [ ] Verify email (if verification enabled)
- [ ] Login and get access token
- [ ] Authorize Swagger with token
- [ ] Run MFA setup endpoint
- [ ] Save backup codes
- [ ] Display QR code
- [ ] Scan QR code with authenticator app
- [ ] Verify 6-digit code appears
- [ ] Enter code to enable MFA
- [ ] Check confirmation email
- [ ] Test login with MFA code
- [ ] Test login with backup code
- [ ] Test wrong code (should fail)
- [ ] Test expired code (wait 30s)
- [ ] Test time sync issue (change phone time)
- [ ] Test disable MFA
- [ ] Test re-enable MFA

---

## Advanced Testing Scenarios

### Test Account Lockout

```bash
# Attempt login with wrong MFA code 5+ times
# Should trigger account lockout
POST /api/auth/login
{
  "email": "test@test.com",
  "password": "Test1234!",
  "mfaCode": "000000"
}
```

### Test MFA with Multiple Devices

1. Setup MFA on Device A
2. Use same QR code / manual key on Device B
3. Both devices generate same codes
4. Test login with codes from both devices

### Test Clock Skew Tolerance

TOTP allows ±1 time window (90 seconds total):
- Previous code (0-30 seconds ago): Valid
- Current code (0-30 seconds): Valid
- Next code (0-30 seconds future): Valid

---

## Related Documentation

- [EMAIL_VERIFICATION_GUIDE.md](./EMAIL_VERIFICATION_GUIDE.md)
- [GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md](./GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md)
- [SECURITY_FEATURES_DETAILED_DOCUMENTATION.md](./SECURITY_FEATURES_DETAILED_DOCUMENTATION.md)

---

## Useful Tools

### QR Code Viewer

Located at: `C:\Gearify\view-qr-code.html`

Open it to easily view QR codes from API responses.

### Authenticator Apps Comparison

| App | Cloud Backup | Multi-Device | Offline | Free |
|-----|-------------|--------------|---------|------|
| Google Authenticator | iOS only | No | Yes | Yes |
| Microsoft Authenticator | Yes | Yes | Yes | Yes |
| Authy | Yes | Yes | Yes | Yes |
| 1Password | Yes | Yes | Yes | Paid |

**Recommendation**: Microsoft Authenticator or Authy for cloud backup

---

## Support

If you encounter issues:
1. Check time synchronization on your phone
2. Review this troubleshooting section
3. Verify auth service is running
4. Check Swagger for detailed error messages
5. Review auth service logs

---

**Last Updated**: December 2025
**Version**: 1.0
