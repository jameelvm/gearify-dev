# Enterprise Security Features - Detailed Documentation

## Table of Contents
1. [Model Classes & Properties](#model-classes--properties)
2. [Sequence Diagrams by Feature](#sequence-diagrams-by-feature)
3. [API Request/Response Models](#api-requestresponse-models)
4. [Configuration Models](#configuration-models)

---

## Model Classes & Properties

### 1. User Entity (Extended)
**Location**: `Domain/Entities/User.cs`

**Purpose**: Stores all user information including authentication, security, and MFA settings

| Property | Type | Description | Used By |
|----------|------|-------------|---------|
| `Id` | string | Unique user identifier (GUID) | All features |
| `TenantId` | string | Multi-tenant identifier | All features |
| `Email` | string | User's email address (lowercase) | Login, Password Reset |
| `PasswordHash` | string | BCrypt hashed password | Login, Password Policy |
| `FirstName` | string | User's first name | Email templates |
| `LastName` | string | User's last name | Email templates |
| `Phone` | string | User's phone number (E.164 format) | SMS MFA |
| `Role` | string | User role (Admin, Customer, Manager) | Authorization |
| `CreatedAt` | DateTime | Account creation timestamp | Audit |
| `UpdatedAt` | DateTime | Last update timestamp | Audit |
| `LastLoginAt` | DateTime? | Last successful login | Audit, Sessions |
| `IsActive` | bool | Account active status | Login |
| `EmailVerified` | bool | Email verification status | Registration |
| `RefreshToken` | string? | Current refresh token | Login, Sessions |
| `RefreshTokenExpiry` | DateTime? | Refresh token expiration | Login, Sessions |
| `EmailVerificationToken` | string? | Email verification token | Registration |
| `EmailVerificationTokenExpiry` | DateTime? | Token expiration | Registration |
| **MFA Fields** | | | |
| `MfaEnabled` | bool | Whether MFA is enabled | Login, MFA |
| `PreferredMfaMethod` | string | Preferred MFA method (None/Totp/Email/Sms) | MFA |
| `TotpSecret` | string? | TOTP secret for authenticator apps | TOTP MFA |
| `BackupCodes` | string? | Comma-separated hashed backup codes | MFA Recovery |
| `LastMfaSetupAt` | DateTime? | When MFA was last configured | Audit |
| **Password Reset Fields** | | | |
| `PasswordResetToken` | string? | Password reset token | Password Reset |
| `PasswordResetTokenExpiry` | DateTime? | Token expiration | Password Reset |
| `LastPasswordChangeAt` | DateTime? | Last password change timestamp | Password Policy |
| **Account Lockout Fields** | | | |
| `FailedLoginAttempts` | int | Count of failed login attempts | Account Lockout |
| `LockoutEnd` | DateTime? | When lockout ends | Account Lockout |
| `LockoutEnabled` | bool | Whether lockout is enabled | Account Lockout |
| **Password History** | | | |
| `PasswordHistory` | string? | Comma-separated hashed passwords | Password Policy |
| **Session Tracking** | | | |
| `ActiveSessionCount` | int | Count of active sessions | Session Management |

---

### 2. UserSession Entity
**Location**: `Domain/Entities/UserSession.cs`

**Purpose**: Tracks individual user sessions for security and management

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique session identifier (GUID) |
| `UserId` | string | Associated user ID |
| `TenantId` | string | Tenant identifier |
| `RefreshToken` | string | Session refresh token |
| `DeviceInfo` | string | User agent / device information |
| `IpAddress` | string | IP address of the session |
| `Location` | string? | Geographic location (optional) |
| `CreatedAt` | DateTime | Session creation time |
| `LastAccessedAt` | DateTime | Last activity timestamp |
| `ExpiresAt` | DateTime | Session expiration time |
| `IsActive` | bool | Whether session is active |

**DynamoDB Schema**:
- **PK**: `USER#{userId}`
- **SK**: `SESSION#{sessionId}`

---

### 3. MfaCode Entity
**Location**: `Domain/Entities/MfaCode.cs`

**Purpose**: Stores temporary OTP codes for Email/SMS MFA

| Property | Type | Description |
|----------|------|-------------|
| `Id` | string | Unique code identifier (GUID) |
| `UserId` | string | Associated user ID |
| `TenantId` | string | Tenant identifier |
| `CodeHash` | string | BCrypt hashed OTP code |
| `Method` | MfaMethod | Email or Sms |
| `CreatedAt` | DateTime | Code generation time |
| `ExpiresAt` | DateTime | Code expiration (typically 5 mins) |
| `IsUsed` | bool | Whether code has been used |
| `AttemptCount` | int | Number of verification attempts |
| `Purpose` | string | Login, Setup, etc. |

**DynamoDB Schema**:
- **PK**: `USER#{userId}`
- **SK**: `MFACODE#{codeId}`

---

### 4. MfaMethod Enum
**Location**: `Domain/Enums/MfaMethod.cs`

```csharp
public enum MfaMethod
{
    None = 0,   // No MFA enabled
    Totp = 1,   // Authenticator app (Google Authenticator, Authy, etc.)
    Email = 2,  // Email OTP codes
    Sms = 3     // SMS OTP codes
}
```

---

### 5. Result Models

#### PasswordValidationResult
**Location**: `Application/Models/PasswordValidationResult.cs`

| Property | Type | Description |
|----------|------|-------------|
| `IsValid` | bool | Whether password meets policy |
| `Errors` | List\<string\> | List of validation errors |

#### MfaSetupResult
**Location**: `Application/Models/MfaSetupResult.cs`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | bool | Whether setup was successful |
| `Message` | string | Success/error message |
| `QrCodeBase64` | string? | Base64-encoded QR code image (TOTP only) |
| `ManualEntryKey` | string? | Formatted TOTP secret for manual entry |
| `BackupCodes` | string[]? | Array of backup recovery codes |

#### MfaVerificationResult
**Location**: `Application/Models/MfaSetupResult.cs`

| Property | Type | Description |
|----------|------|-------------|
| `Success` | bool | Whether verification was successful |
| `Message` | string | Success/error message |
| `Token` | string? | Access token (if MFA verification during login) |
| `RefreshToken` | string? | Refresh token (if MFA verification during login) |

#### SessionInfo
**Location**: `Application/Models/SessionInfo.cs`

| Property | Type | Description |
|----------|------|-------------|
| `SessionId` | string | Session identifier |
| `DeviceInfo` | string | Device/browser information |
| `IpAddress` | string | IP address |
| `Location` | string? | Geographic location |
| `CreatedAt` | DateTime | When session was created |
| `LastAccessedAt` | DateTime | Last activity time |
| `ExpiresAt` | DateTime | When session expires |
| `IsCurrent` | bool | Whether this is the current session |

---

### 6. Configuration Models
**Location**: `Application/Models/SecurityConfiguration.cs`

#### SecurityConfiguration
```csharp
public class SecurityConfiguration
{
    public PasswordPolicySettings PasswordPolicy { get; set; }
    public AccountLockoutSettings AccountLockout { get; set; }
    public MfaSettings Mfa { get; set; }
    public PasswordResetSettings PasswordReset { get; set; }
    public SessionSettings Session { get; set; }
}
```

#### PasswordPolicySettings
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MinimumLength` | int | 8 | Minimum password length |
| `RequireUppercase` | bool | true | Require uppercase letters |
| `RequireLowercase` | bool | true | Require lowercase letters |
| `RequireDigit` | bool | true | Require numbers |
| `RequireSpecialChar` | bool | true | Require special characters |
| `PasswordHistoryCount` | int | 5 | Number of previous passwords to remember |

#### AccountLockoutSettings
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxFailedAttempts` | int | 5 | Failed attempts before lockout |
| `LockoutDurationMinutes` | int | 30 | Duration of lockout |
| `EnableLockout` | bool | true | Whether lockout is enabled |

#### MfaSettings
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CodeExpiryMinutes` | int | 5 | OTP code expiration time |
| `MaxVerificationAttempts` | int | 3 | Max attempts per code |
| `BackupCodesCount` | int | 10 | Number of backup codes |
| `TotpIssuer` | string | "Gearify" | Issuer name in authenticator apps |
| `TotpDigits` | int | 6 | Number of digits in TOTP code |
| `TotpPeriod` | int | 30 | TOTP time step in seconds |

#### PasswordResetSettings
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TokenExpiryHours` | int | 1 | Reset token expiration |
| `MaxResetAttemptsPerDay` | int | 3 | Max reset requests per day |

#### SessionSettings
| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxConcurrentSessions` | int | 5 | Maximum simultaneous sessions |
| `SessionTimeoutMinutes` | int | 60 | Session inactivity timeout |
| `RefreshTokenExpiryDays` | int | 7 | Refresh token expiration |

---

## Sequence Diagrams by Feature

### Feature 1: User Registration with Password Policy

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant RegisterHandler
    participant PasswordPolicy
    participant UserRepo
    participant EmailService

    Client->>API: POST /api/auth/register
    API->>RegisterHandler: RegisterUserCommand

    RegisterHandler->>PasswordPolicy: ValidatePassword(password)
    alt Password Invalid
        PasswordPolicy-->>RegisterHandler: ValidationResult(false, errors)
        RegisterHandler-->>Client: 400 Bad Request (errors)
    end

    PasswordPolicy-->>RegisterHandler: ValidationResult(true)
    RegisterHandler->>PasswordPolicy: HashPassword(password)
    PasswordPolicy-->>RegisterHandler: passwordHash

    RegisterHandler->>UserRepo: GetByEmailAsync(email)
    alt User Exists
        UserRepo-->>RegisterHandler: User
        RegisterHandler-->>Client: 400 Bad Request (Email exists)
    end

    UserRepo-->>RegisterHandler: null
    RegisterHandler->>PasswordPolicy: AddToPasswordHistory(hash, user)
    RegisterHandler->>UserRepo: CreateAsync(user)
    UserRepo-->>RegisterHandler: Success

    RegisterHandler->>EmailService: SendWelcomeEmail(user)
    EmailService-->>RegisterHandler: Success

    RegisterHandler-->>Client: 200 OK (tokens)
```

---

### Feature 2: Login with Account Lockout

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant LoginHandler
    participant UserRepo
    participant LockoutService
    participant PasswordHasher
    participant EmailService
    participant JwtService

    Client->>API: POST /api/auth/login
    API->>LoginHandler: LoginCommand(email, password)

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    alt User Not Found
        UserRepo-->>LoginHandler: null
        LoginHandler-->>Client: 401 Unauthorized
    end

    UserRepo-->>LoginHandler: User
    LoginHandler->>LockoutService: IsLockedOut(user)

    alt Account Locked
        LockoutService-->>LoginHandler: true (remaining time)
        LoginHandler-->>Client: 401 (Account locked, try in X mins)
    end

    LockoutService-->>LoginHandler: false
    LoginHandler->>PasswordHasher: VerifyPassword(password, hash)

    alt Password Invalid
        PasswordHasher-->>LoginHandler: false
        LoginHandler->>LockoutService: RecordFailedLoginAttempt(user)

        alt Account Now Locked
            LockoutService-->>LoginHandler: true (locked)
            LoginHandler->>UserRepo: UpdateAsync(user)
            LoginHandler->>EmailService: SendAccountLockedEmail(user)
            LoginHandler-->>Client: 401 (Account locked)
        else Not Locked Yet
            LockoutService-->>LoginHandler: false
            LoginHandler->>UserRepo: UpdateAsync(user)
            LoginHandler-->>Client: 401 (Invalid credentials)
        end
    end

    PasswordHasher-->>LoginHandler: true
    LoginHandler->>LockoutService: ResetFailedLoginAttempts(user)
    LoginHandler->>JwtService: GenerateAccessToken(user)
    JwtService-->>LoginHandler: accessToken
    LoginHandler->>JwtService: GenerateRefreshToken()
    JwtService-->>LoginHandler: refreshToken

    LoginHandler->>UserRepo: UpdateAsync(user)
    LoginHandler-->>Client: 200 OK (tokens)
```

---

### Feature 3: Password Reset Flow

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant ForgotPwdHandler
    participant ResetPwdHandler
    participant UserRepo
    participant PasswordPolicy
    participant EmailService
    participant Config

    Note over Client,Config: Step 1: Request Password Reset
    Client->>API: POST /api/password/forgot
    API->>ForgotPwdHandler: ForgotPasswordCommand(email)

    ForgotPwdHandler->>UserRepo: GetByEmailAsync(email)
    alt User Not Found
        UserRepo-->>ForgotPwdHandler: null
        ForgotPwdHandler-->>Client: 200 OK (generic message)
        Note over ForgotPwdHandler,Client: Prevent user enumeration
    end

    UserRepo-->>ForgotPwdHandler: User
    ForgotPwdHandler->>ForgotPwdHandler: GenerateSecureToken()
    ForgotPwdHandler->>Config: Get TokenExpiryHours
    Config-->>ForgotPwdHandler: 1 hour

    ForgotPwdHandler->>UserRepo: UpdateAsync(user with token)
    ForgotPwdHandler->>EmailService: SendPasswordResetEmail(user, resetLink)
    EmailService-->>ForgotPwdHandler: Success
    ForgotPwdHandler-->>Client: 200 OK (generic message)

    Note over Client,Config: Step 2: Reset Password with Token
    Client->>API: POST /api/password/reset
    API->>ResetPwdHandler: ResetPasswordCommand(email, token, newPassword)

    ResetPwdHandler->>UserRepo: GetByEmailAsync(email)
    alt User Not Found
        UserRepo-->>ResetPwdHandler: null
        ResetPwdHandler-->>Client: 400 Bad Request
    end

    UserRepo-->>ResetPwdHandler: User

    alt Token Invalid or Expired
        ResetPwdHandler-->>Client: 400 Bad Request (Invalid token)
    end

    ResetPwdHandler->>PasswordPolicy: ValidatePassword(newPassword)
    alt Password Invalid
        PasswordPolicy-->>ResetPwdHandler: ValidationResult(false)
        ResetPwdHandler-->>Client: 400 Bad Request (policy errors)
    end

    PasswordPolicy-->>ResetPwdHandler: ValidationResult(true)
    ResetPwdHandler->>PasswordPolicy: IsPasswordInHistory(newPassword)
    alt In History
        PasswordPolicy-->>ResetPwdHandler: true
        ResetPwdHandler-->>Client: 400 Bad Request (reused password)
    end

    PasswordPolicy-->>ResetPwdHandler: false
    ResetPwdHandler->>PasswordPolicy: HashPassword(newPassword)
    ResetPwdHandler->>PasswordPolicy: AddToPasswordHistory(oldHash)

    ResetPwdHandler->>UserRepo: UpdateAsync(user)
    ResetPwdHandler->>EmailService: SendPasswordResetSuccessEmail(user)
    ResetPwdHandler-->>Client: 200 OK (Success message)
```

---

### Feature 4: TOTP MFA Setup

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SetupHandler
    participant VerifyHandler
    participant UserRepo
    participant TotpService
    participant EmailService

    Note over Client,EmailService: Step 1: Initiate TOTP Setup
    Client->>API: POST /api/mfa/setup/totp [Authenticated]
    API->>SetupHandler: SetupTotpMfaCommand(userId)

    SetupHandler->>UserRepo: GetByIdAsync(userId)
    UserRepo-->>SetupHandler: User

    SetupHandler->>TotpService: GenerateSecret()
    TotpService-->>SetupHandler: secret (Base32)

    SetupHandler->>TotpService: GenerateQrCode(email, secret)
    TotpService-->>SetupHandler: qrCodeBase64

    SetupHandler->>TotpService: FormatSecretForDisplay(secret)
    TotpService-->>SetupHandler: formattedSecret

    SetupHandler->>SetupHandler: GenerateBackupCodes(10)
    SetupHandler->>SetupHandler: HashBackupCodes()

    SetupHandler->>UserRepo: UpdateAsync(user with secret & codes)
    Note over SetupHandler: MFA not enabled yet

    SetupHandler-->>Client: 200 OK {qrCode, secret, backupCodes}

    Note over Client: User scans QR code with authenticator app

    Note over Client,EmailService: Step 2: Verify and Enable MFA
    Client->>API: POST /api/mfa/verify (code from app)
    API->>VerifyHandler: VerifyMfaSetupCommand(userId, code)

    VerifyHandler->>UserRepo: GetByIdAsync(userId)
    UserRepo-->>VerifyHandler: User

    alt No TOTP Secret
        VerifyHandler-->>Client: 400 Bad Request (Setup first)
    end

    VerifyHandler->>TotpService: VerifyCode(secret, code)

    alt Code Invalid
        TotpService-->>VerifyHandler: false
        VerifyHandler-->>Client: 400 Bad Request (Invalid code)
    end

    TotpService-->>VerifyHandler: true
    VerifyHandler->>UserRepo: UpdateAsync(user, MfaEnabled=true)
    VerifyHandler->>EmailService: SendMfaEnabledEmail(user)
    VerifyHandler-->>Client: 200 OK (MFA enabled)
```

---

### Feature 5: Email OTP MFA (Login)

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant LoginHandler
    participant OtpService
    participant MfaRepo
    participant EmailService
    participant UserRepo

    Note over Client,UserRepo: Assume user has Email MFA enabled

    Client->>API: POST /api/auth/login (email, password)
    API->>LoginHandler: LoginCommand

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    LoginHandler->>LoginHandler: Verify password (success)

    alt MFA Enabled (Email)
        LoginHandler->>OtpService: GenerateCode()
        OtpService-->>LoginHandler: 6-digit code

        LoginHandler->>OtpService: StoreCodeAsync(userId, code, Email, Login, 5mins)
        OtpService->>MfaRepo: CreateAsync(MfaCode)

        LoginHandler->>EmailService: SendOtpEmail(user, code)
        EmailService-->>LoginHandler: Success

        LoginHandler-->>Client: 200 OK {requiresMfa: true, method: Email}

        Note over Client: User receives email with code

        Client->>API: POST /api/mfa/verify-login (userId, code)
        API->>LoginHandler: VerifyMfaLoginCommand

        LoginHandler->>OtpService: VerifyCodeAsync(userId, code, Login)
        OtpService->>MfaRepo: GetByUserAndCodeAsync(userId, hash)

        alt Code Not Found or Expired
            MfaRepo-->>OtpService: null
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized
        end

        MfaRepo-->>OtpService: MfaCode

        alt Max Attempts Exceeded
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 (Too many attempts)
        end

        OtpService-->>LoginHandler: true
        LoginHandler->>OtpService: InvalidateCodeAsync(userId, code)
        OtpService->>MfaRepo: UpdateAsync(IsUsed=true)

        LoginHandler->>LoginHandler: Generate JWT tokens
        LoginHandler-->>Client: 200 OK (access & refresh tokens)
    end
```

---

### Feature 6: Session Management

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SessionService
    participant SessionRepo
    participant UserRepo

    Note over Client,UserRepo: Get Active Sessions
    Client->>API: GET /api/session/active [Authenticated]
    API->>SessionService: GetActiveSessionsAsync(userId)

    SessionService->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo-->>SessionService: List<UserSession>

    SessionService->>SessionService: Map to SessionInfo
    SessionService-->>Client: 200 OK [sessions array]

    Note over Client,UserRepo: Revoke Specific Session
    Client->>API: POST /api/session/revoke/{sessionId}
    API->>SessionService: RevokeSessionAsync(userId, sessionId)

    SessionService->>SessionRepo: GetByIdAsync(sessionId, userId)
    alt Session Not Found
        SessionRepo-->>SessionService: null
        SessionService-->>Client: 404 Not Found
    end

    SessionRepo-->>SessionService: UserSession
    SessionService->>SessionRepo: UpdateAsync(session, IsActive=false)
    SessionService-->>Client: 200 OK (Session revoked)

    Note over Client,UserRepo: Revoke All Sessions (Logout Everywhere)
    Client->>API: POST /api/session/revoke-all
    API->>SessionService: RevokeAllSessionsAsync(userId)

    SessionService->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo-->>SessionService: List<UserSession>

    loop For each session
        SessionService->>SessionRepo: UpdateAsync(session, IsActive=false)
    end

    SessionService-->>Client: 200 OK (All sessions revoked)

    Note over Client,UserRepo: Create Session on Login
    Client->>API: POST /api/auth/login
    API->>SessionService: CreateSessionAsync(userId, refreshToken, deviceInfo, ip)

    SessionService->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo-->>SessionService: List<UserSession> (count)

    alt Exceeds Max Sessions (5)
        SessionService->>SessionRepo: DeleteAsync(oldestSession)
    end

    SessionService->>SessionRepo: CreateAsync(newSession)
    SessionService-->>API: UserSession
```

---

### Feature 7: Change Password (Authenticated User)

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant ChangeHandler
    participant UserRepo
    participant PasswordPolicy
    participant EmailService

    Client->>API: POST /api/password/change [Authenticated]
    API->>ChangeHandler: ChangePasswordCommand(userId, currentPwd, newPwd)

    ChangeHandler->>UserRepo: GetByIdAsync(userId)
    UserRepo-->>ChangeHandler: User

    ChangeHandler->>PasswordPolicy: VerifyPassword(currentPwd, hash)
    alt Current Password Invalid
        PasswordPolicy-->>ChangeHandler: false
        ChangeHandler-->>Client: 400 Bad Request (Wrong password)
    end

    PasswordPolicy-->>ChangeHandler: true
    ChangeHandler->>PasswordPolicy: ValidatePassword(newPwd)
    alt New Password Invalid
        PasswordPolicy-->>ChangeHandler: ValidationResult(false, errors)
        ChangeHandler-->>Client: 400 Bad Request (errors)
    end

    PasswordPolicy-->>ChangeHandler: ValidationResult(true)

    ChangeHandler->>PasswordPolicy: VerifyPassword(newPwd, currentHash)
    alt Same as Current
        PasswordPolicy-->>ChangeHandler: true
        ChangeHandler-->>Client: 400 Bad Request (Must be different)
    end

    PasswordPolicy-->>ChangeHandler: false
    ChangeHandler->>PasswordPolicy: IsPasswordInHistory(newPwd)
    alt In History
        PasswordPolicy-->>ChangeHandler: true
        ChangeHandler-->>Client: 400 Bad Request (Recently used)
    end

    PasswordPolicy-->>ChangeHandler: false
    ChangeHandler->>PasswordPolicy: HashPassword(newPwd)
    PasswordPolicy-->>ChangeHandler: newHash

    ChangeHandler->>PasswordPolicy: AddToPasswordHistory(currentHash)
    ChangeHandler->>UserRepo: UpdateAsync(user)
    Note over ChangeHandler: Also invalidates refresh tokens

    ChangeHandler->>EmailService: SendPasswordChangedEmail(user)
    ChangeHandler-->>Client: 200 OK (Password changed)
```

---

## API Request/Response Models

### 1. Password Management

#### Forgot Password Request
```json
POST /api/password/forgot
{
  "email": "user@example.com"
}

Response: 200 OK
{
  "success": true,
  "message": "If an account exists with this email, you will receive a password reset link."
}
```

#### Reset Password Request
```json
POST /api/password/reset
{
  "email": "user@example.com",
  "resetToken": "base64-encoded-token",
  "newPassword": "NewSecure@Pass123"
}

Response: 200 OK
{
  "success": true,
  "message": "Your password has been reset successfully."
}

Error: 400 Bad Request
{
  "success": false,
  "message": "Password must contain at least one uppercase letter. Password must contain at least one special character."
}
```

#### Change Password Request
```json
POST /api/password/change
Authorization: Bearer {token}
{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecure@Pass456"
}

Response: 200 OK
{
  "success": true,
  "message": "Your password has been changed successfully."
}
```

---

### 2. MFA Management

#### Setup TOTP MFA
```json
POST /api/mfa/setup/totp
Authorization: Bearer {token}

Response: 200 OK
{
  "success": true,
  "message": "TOTP MFA setup successful.",
  "qrCodeBase64": "iVBORw0KGgoAAAANSUhEUgAA...",
  "manualEntryKey": "JBSW Y3DP EHPK 3PXP",
  "backupCodes": [
    "ABCD-1234",
    "EFGH-5678",
    "IJKL-9012",
    ...
  ]
}
```

#### Verify MFA Setup
```json
POST /api/mfa/verify
Authorization: Bearer {token}
{
  "code": "123456"
}

Response: 200 OK
{
  "success": true,
  "message": "MFA has been enabled successfully."
}
```

#### Disable MFA
```json
POST /api/mfa/disable
Authorization: Bearer {token}
{
  "password": "CurrentPassword123!"
}

Response: 200 OK
{
  "success": true,
  "message": "MFA has been disabled successfully."
}
```

---

### 3. Session Management

#### Get Active Sessions
```json
GET /api/session/active
Authorization: Bearer {token}

Response: 200 OK
{
  "success": true,
  "sessions": [
    {
      "sessionId": "sess-123",
      "deviceInfo": "Chrome 120.0 on Windows 10",
      "ipAddress": "192.168.1.100",
      "location": "New York, USA",
      "createdAt": "2025-10-26T10:00:00Z",
      "lastAccessedAt": "2025-10-26T14:30:00Z",
      "expiresAt": "2025-11-02T10:00:00Z",
      "isCurrent": true
    },
    {
      "sessionId": "sess-456",
      "deviceInfo": "Safari 17.0 on iPhone",
      "ipAddress": "192.168.1.101",
      "location": "New York, USA",
      "createdAt": "2025-10-25T08:00:00Z",
      "lastAccessedAt": "2025-10-26T12:00:00Z",
      "expiresAt": "2025-11-01T08:00:00Z",
      "isCurrent": false
    }
  ]
}
```

#### Revoke Session
```json
POST /api/session/revoke/sess-456
Authorization: Bearer {token}

Response: 200 OK
{
  "success": true,
  "message": "Session revoked successfully."
}
```

#### Revoke All Sessions
```json
POST /api/session/revoke-all
Authorization: Bearer {token}

Response: 200 OK
{
  "success": true,
  "message": "All sessions revoked successfully."
}
```

---

## Error Response Format

All endpoints follow consistent error response format:

```json
400 Bad Request / 401 Unauthorized
{
  "success": false,
  "message": "Detailed error message explaining what went wrong"
}
```

**Common Error Scenarios**:

| HTTP Code | Scenario | Message Example |
|-----------|----------|----------------|
| 400 | Password policy violation | "Password must be at least 8 characters long." |
| 400 | Password in history | "You cannot reuse a recent password." |
| 401 | Account locked | "Account is locked. Please try again in 28 minutes." |
| 401 | Invalid credentials | "Invalid email or password" |
| 401 | MFA code invalid | "Invalid verification code. Please try again." |
| 401 | Token expired | "Reset token has expired. Please request a new one." |
| 404 | Session not found | "Session not found." |

---

## Summary

This documentation provides:
- ✅ Complete model class definitions with all properties
- ✅ Property associations showing which features use each field
- ✅ Detailed sequence diagrams for all 7 major features
- ✅ API request/response examples
- ✅ Error handling patterns
- ✅ Configuration model details

All diagrams use Mermaid syntax and can be rendered in:
- GitHub (native support)
- GitLab (native support)
- VS Code (with Mermaid extension)
- Documentation tools (MkDocs, Docusaurus, etc.)

---

**Document Version**: 1.0
**Last Updated**: October 26, 2025
**Status**: ✅ Complete
