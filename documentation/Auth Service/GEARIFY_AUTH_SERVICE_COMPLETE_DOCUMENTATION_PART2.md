# Gearify Authentication Microservice - Complete Documentation (Part 2)

**This is a continuation of GEARIFY_AUTH_SERVICE_COMPLETE_DOCUMENTATION.md**

---

## 4. Features & Functionality (Continued)

### 4.6 Multi-Factor Authentication (MFA)

**Endpoints**:
- `POST /api/mfa/setup/totp` - Setup TOTP authenticator app
- `POST /api/mfa/verify` - Verify and enable MFA
- `POST /api/mfa/disable` - Disable MFA

**Purpose**: Add an additional layer of security beyond passwords.

#### 4.6.1 Feature Overview

MFA provides three verification methods:
1. **TOTP (Time-based One-Time Password)**: Authenticator apps (Google Authenticator, Microsoft Authenticator, Authy)
2. **Email OTP**: 6-digit codes sent via email
3. **SMS OTP**: 6-digit codes sent via SMS (AWS SNS)
4. **Backup Codes**: 10 single-use recovery codes

#### 4.6.2 Sequence Diagram - TOTP MFA Setup

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SetupHandler
    participant UserRepo
    participant TotpService
    participant QRCoder
    participant EmailService
    participant DynamoDB

    Note over Client: User in security settings
    Note over Client: Clicks "Enable MFA"

    Client->>API: POST /api/mfa/setup/totp<br/>Authorization: Bearer {token}
    API->>SetupHandler: SetupTotpMfaCommand

    SetupHandler->>SetupHandler: Extract userId from JWT

    Note over SetupHandler: Step 1: Load User
    SetupHandler->>UserRepo: GetByIdAsync(userId)
    UserRepo->>DynamoDB: GetItem
    DynamoDB-->>UserRepo: User
    UserRepo-->>SetupHandler: User

    alt MFA Already Enabled
        SetupHandler-->>Client: 400 Bad Request<br/>{error: "MFA already enabled"}
    end

    Note over SetupHandler: Step 2: Generate TOTP Secret
    SetupHandler->>TotpService: GenerateSecret()
    TotpService->>TotpService: Generate 32-byte random key
    TotpService->>TotpService: Base32 encode
    TotpService-->>SetupHandler: secret (e.g., "JBSWY3DPEHPK3PXP")

    Note over SetupHandler: Step 3: Generate QR Code
    SetupHandler->>TotpService: GenerateQrCode(email, secret, issuer)
    TotpService->>TotpService: Build TOTP URI<br/>otpauth://totp/Gearify:user@example.com?secret=XXX&issuer=Gearify
    TotpService->>QRCoder: GenerateQrCode(uri)
    QRCoder->>QRCoder: Create QR code image (PNG)
    QRCoder-->>TotpService: QR code bytes
    TotpService->>TotpService: Convert to Base64
    TotpService-->>SetupHandler: qrCodeBase64

    Note over SetupHandler: Step 4: Format Secret for Manual Entry
    SetupHandler->>TotpService: FormatSecretForDisplay(secret)
    TotpService->>TotpService: Add spaces every 4 chars<br/>"JBSW Y3DP EHPK 3PXP"
    TotpService-->>SetupHandler: formattedSecret

    Note over SetupHandler: Step 5: Generate Backup Codes
    SetupHandler->>SetupHandler: Generate 10 backup codes
    SetupHandler->>SetupHandler: Format: "ABCD-1234"
    SetupHandler->>SetupHandler: Hash codes with BCrypt
    SetupHandler->>SetupHandler: Join hashes with comma

    Note over SetupHandler: Step 6: Save to User (Not Enabled Yet)
    SetupHandler->>SetupHandler: Set user.TotpSecret = secret
    SetupHandler->>SetupHandler: Set user.BackupCodes = hashedCodes
    SetupHandler->>SetupHandler: Set user.LastMfaSetupAt = Now
    SetupHandler->>SetupHandler: Keep user.MfaEnabled = false

    SetupHandler->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem
    DynamoDB-->>UserRepo: Updated

    SetupHandler-->>Client: 200 OK<br/>{qrCode, manualKey, backupCodes}

    Note over Client: Client displays QR code
    Note over Client: User scans with authenticator app
    Note over Client: User saves backup codes securely
    Note over Client: User enters code from app to verify

    Client->>API: POST /api/mfa/verify<br/>{code: "123456"}
    API->>API: VerifyMfaSetupCommand

    API->>UserRepo: GetByIdAsync(userId)
    UserRepo->>DynamoDB: GetItem
    DynamoDB-->>UserRepo: User
    UserRepo-->>API: User

    alt No TOTP Secret Configured
        API-->>Client: 400 Bad Request<br/>{error: "MFA setup not initiated"}
    end

    Note over API: Verify TOTP Code
    API->>TotpService: VerifyCode(user.TotpSecret, code)
    TotpService->>TotpService: Get current Unix timestamp
    TotpService->>TotpService: Calculate time step (30 seconds)
    TotpService->>TotpService: Generate expected code for current window
    TotpService->>TotpService: Check previous/next window (time drift tolerance)

    alt Code Invalid
        TotpService-->>API: false
        API-->>Client: 400 Bad Request<br/>{error: "Invalid verification code"}
    end

    TotpService-->>API: true (code valid)

    Note over API: Enable MFA
    API->>API: Set user.MfaEnabled = true
    API->>API: Set user.PreferredMfaMethod = "Totp"

    API->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem

    Note over API: Send Confirmation Email
    API->>EmailService: SendMfaEnabledEmail(user)
    EmailService->>EmailService: Render template
    EmailService->>EmailService: Send via SES

    API-->>Client: 200 OK<br/>{success: true, message: "MFA enabled"}

    Note over Client: User now requires MFA on login
```

#### 4.6.3 Sequence Diagram - MFA Verification During Login

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant LoginHandler
    participant UserRepo
    participant TotpService
    participant OtpService
    participant MfaRepo
    participant SmsService
    participant EmailService
    participant DynamoDB

    Note over Client: User logs in with password

    Client->>API: POST /api/auth/login<br/>{email, password}
    API->>LoginHandler: LoginCommand

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    UserRepo-->>LoginHandler: User

    LoginHandler->>LoginHandler: Verify password (success)
    LoginHandler->>LoginHandler: Check user.MfaEnabled == true

    alt MFA Method: TOTP
        Note over LoginHandler: User has authenticator app

        LoginHandler-->>Client: 200 OK<br/>{requiresMfa: true, method: "Totp", userId}

        Note over Client: User opens authenticator app
        Note over Client: User sees 6-digit code (refreshes every 30 sec)

        Client->>API: POST /api/mfa/verify-login<br/>{userId, code: "456789"}
        API->>LoginHandler: VerifyMfaLoginCommand

        LoginHandler->>UserRepo: GetByIdAsync(userId)
        UserRepo-->>LoginHandler: User

        LoginHandler->>TotpService: VerifyCode(user.TotpSecret, code)
        TotpService->>TotpService: Calculate expected code

        alt Invalid TOTP Code
            TotpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid code"}
        end

        TotpService-->>LoginHandler: true
        LoginHandler->>LoginHandler: Generate JWT tokens
        LoginHandler-->>Client: 200 OK<br/>{token, refreshToken, user}

    else MFA Method: Email
        Note over LoginHandler: User prefers email OTP

        Note over LoginHandler: Generate OTP Code
        LoginHandler->>OtpService: GenerateCode()
        OtpService->>OtpService: Generate random 6-digit code
        OtpService-->>LoginHandler: "123456"

        Note over LoginHandler: Store OTP Code
        LoginHandler->>OtpService: StoreCodeAsync(userId, code, Email, Login, 5mins)
        OtpService->>OtpService: Hash code with BCrypt
        OtpService->>MfaRepo: CreateAsync(MfaCode entity)
        MfaRepo->>DynamoDB: PutItem<br/>PK: USER#{userId}<br/>SK: MFACODE#{codeId}<br/>TTL: 5 minutes

        Note over LoginHandler: Send Email
        LoginHandler->>EmailService: SendOtpEmail(user, code)
        EmailService->>EmailService: Render "MFA Code" template
        EmailService->>EmailService: Replace {{Code}} = "123456"
        EmailService->>EmailService: Send via AWS SES

        LoginHandler-->>Client: 200 OK<br/>{requiresMfa: true, method: "Email", userId}

        Note over Client: User checks email inbox
        Note over Client: User receives code "123456"

        Client->>API: POST /api/mfa/verify-login<br/>{userId, code: "123456"}
        API->>LoginHandler: VerifyMfaLoginCommand

        LoginHandler->>OtpService: VerifyCodeAsync(userId, code, Login)
        OtpService->>MfaRepo: GetByUserAsync(userId, purpose=Login)
        MfaRepo->>DynamoDB: Query<br/>PK: USER#{userId}<br/>Filter: IsUsed=false, ExpiresAt>Now
        DynamoDB-->>MfaRepo: MfaCode entity
        MfaRepo-->>OtpService: MfaCode

        alt Code Expired
            OtpService->>OtpService: Check ExpiresAt < Now
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Code expired"}
        end

        alt Max Attempts Exceeded
            OtpService->>OtpService: Check AttemptCount >= 3
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Too many attempts"}
        end

        OtpService->>OtpService: BCrypt.Verify(code, mfaCode.CodeHash)

        alt Invalid Code
            OtpService->>OtpService: Increment AttemptCount
            OtpService->>MfaRepo: UpdateAsync(mfaCode)
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid code"}
        end

        Note over OtpService: Code Valid - Mark as Used
        OtpService->>OtpService: Set IsUsed = true
        OtpService->>MfaRepo: UpdateAsync(mfaCode)

        OtpService-->>LoginHandler: true
        LoginHandler->>LoginHandler: Generate JWT tokens
        LoginHandler-->>Client: 200 OK<br/>{token, refreshToken, user}

    else MFA Method: SMS
        Note over LoginHandler: User prefers SMS OTP

        LoginHandler->>OtpService: GenerateCode()
        OtpService-->>LoginHandler: "789012"

        LoginHandler->>OtpService: StoreCodeAsync(userId, code, Sms, Login, 5mins)
        OtpService->>MfaRepo: CreateAsync(MfaCode)

        Note over LoginHandler: Send SMS
        LoginHandler->>SmsService: SendOtpSmsAsync(user.Phone, code)
        SmsService->>SmsService: Format message: "Your Gearify code is: 789012"
        SmsService->>SmsService: Send via AWS SNS
        SmsService-->>LoginHandler: MessageId

        LoginHandler-->>Client: 200 OK<br/>{requiresMfa: true, method: "Sms", userId}

        Note over Client: User receives SMS
        Note over Client: User enters code

        Client->>API: POST /api/mfa/verify-login<br/>{userId, code: "789012"}
        Note over API: Same verification flow as Email OTP
        API-->>Client: 200 OK<br/>{token, refreshToken, user}
    end
```

#### 4.6.4 Request/Response

**Setup TOTP Request**:
```json
POST /api/mfa/setup/totp
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001
```

**Setup TOTP Response (200 OK)**:
```json
{
  "success": true,
  "message": "TOTP MFA setup initiated. Scan the QR code with your authenticator app.",
  "qrCodeBase64": "iVBORw0KGgoAAAANSUhEUgAA...(base64 encoded PNG image)",
  "manualEntryKey": "JBSW Y3DP EHPK 3PXP",
  "backupCodes": [
    "ABCD-1234",
    "EFGH-5678",
    "IJKL-9012",
    "MNOP-3456",
    "QRST-7890",
    "UVWX-1234",
    "YZAB-5678",
    "CDEF-9012",
    "GHIJ-3456",
    "KLMN-7890"
  ]
}
```

**Verify MFA Setup Request**:
```json
POST /api/mfa/verify
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001

{
  "code": "456789"
}
```

**Verify MFA Setup Response (200 OK)**:
```json
{
  "success": true,
  "message": "Multi-factor authentication has been enabled successfully."
}
```

**Disable MFA Request**:
```json
POST /api/mfa/disable
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001

{
  "password": "CurrentPassword123!"
}
```

**Disable MFA Response (200 OK)**:
```json
{
  "success": true,
  "message": "Multi-factor authentication has been disabled."
}
```

#### 4.6.5 Business Rules

1. **TOTP Configuration**:
   - Algorithm: SHA1 (RFC 6238 standard)
   - Digits: 6
   - Period: 30 seconds
   - Time drift tolerance: ±1 window (allows 30 sec before/after)

2. **QR Code**:
   - Format: otpauth://totp/Gearify:user@example.com?secret=XXX&issuer=Gearify
   - Image size: 250x250 pixels
   - Error correction: Medium level

3. **Backup Codes**:
   - Count: 10 codes
   - Format: 8 alphanumeric characters (ABCD-1234)
   - Storage: BCrypt hashed
   - Usage: Single-use only
   - Shown only once during setup

4. **OTP Codes (Email/SMS)**:
   - Length: 6 digits
   - Character set: 0-9
   - Expiry: 5 minutes
   - Max attempts: 3
   - Hashed with BCrypt before storage

5. **MFA Verification**:
   - Required on every login if enabled
   - Cannot be bypassed
   - "Remember this device" feature not implemented (future)

6. **Disabling MFA**:
   - Requires password verification
   - Sends email notification
   - Clears TotpSecret and BackupCodes
   - Sets MfaEnabled = false

---

### 4.7 Session Management

**Endpoints**:
- `GET /api/session/active` - List active sessions
- `POST /api/session/revoke/{sessionId}` - Revoke specific session
- `POST /api/session/revoke-all` - Revoke all sessions

**Purpose**: Track and manage user sessions across multiple devices.

#### 4.7.1 Feature Overview

Session management provides:
- View all active sessions
- See device and location information
- Revoke suspicious sessions
- Logout from all devices
- Automatic session limit enforcement

#### 4.7.2 Sequence Diagram - View Active Sessions

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SessionController
    participant GetSessionsQuery
    participant SessionRepo
    participant DynamoDB

    Note over Client: User in security settings
    Note over Client: Clicks "Active Sessions"

    Client->>API: GET /api/session/active<br/>Authorization: Bearer {token}
    API->>SessionController: GetActiveSessionsRequest

    SessionController->>SessionController: Extract userId from JWT

    SessionController->>GetSessionsQuery: GetActiveSessionsQuery(userId)

    GetSessionsQuery->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo->>DynamoDB: Query<br/>PK: USER#{userId}<br/>Filter: IsActive=true AND ExpiresAt>Now
    DynamoDB-->>SessionRepo: List of UserSession entities

    SessionRepo-->>GetSessionsQuery: List<UserSession>

    Note over GetSessionsQuery: Map to SessionInfo DTOs
    GetSessionsQuery->>GetSessionsQuery: For each session:<br/>- Extract device info<br/>- Format timestamps<br/>- Mark current session

    GetSessionsQuery-->>SessionController: List<SessionInfo>

    SessionController-->>Client: 200 OK<br/>{sessions: [...]}

    Note over Client: Display session list:
    Note over Client: - Device name
    Note over Client: - Location
    Note over Client: - Last active time
    Note over Client: - "Current device" badge
```

#### 4.7.3 Request/Response

**Get Active Sessions Request**:
```json
GET /api/session/active
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001
```

**Get Active Sessions Response (200 OK)**:
```json
{
  "success": true,
  "sessions": [
    {
      "sessionId": "sess-550e8400-e29b-41d4-a716-446655440000",
      "deviceInfo": "Chrome 120.0 on Windows 10",
      "ipAddress": "192.168.1.100",
      "location": "New York, USA",
      "createdAt": "2025-11-01T10:00:00Z",
      "lastAccessedAt": "2025-11-02T14:30:00Z",
      "expiresAt": "2025-11-08T10:00:00Z",
      "isCurrent": true
    },
    {
      "sessionId": "sess-660f9511-f3ac-52e5-b827-557766551111",
      "deviceInfo": "Safari 17.0 on iPhone 15",
      "ipAddress": "192.168.1.101",
      "location": "New York, USA",
      "createdAt": "2025-10-30T08:00:00Z",
      "lastAccessedAt": "2025-11-02T12:00:00Z",
      "expiresAt": "2025-11-06T08:00:00Z",
      "isCurrent": false
    },
    {
      "sessionId": "sess-770g0622-g4bd-63f6-c938-668877662222",
      "deviceInfo": "Firefox 119.0 on Ubuntu Linux",
      "ipAddress": "203.0.113.42",
      "location": "San Francisco, USA",
      "createdAt": "2025-10-28T15:00:00Z",
      "lastAccessedAt": "2025-11-01T09:00:00Z",
      "expiresAt": "2025-11-04T15:00:00Z",
      "isCurrent": false
    }
  ]
}
```

#### 4.7.4 Session Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Created: Login Success
    Created --> Active: Token Valid
    Active --> Active: Token Refresh
    Active --> Expired: TTL Exceeded
    Active --> Revoked: User Revokes
    Active --> Revoked: Logout
    Active --> Revoked: Password Changed
    Revoked --> [*]
    Expired --> [*]
```

#### 4.7.5 Business Rules

1. **Session Creation**:
   - Created on successful login
   - One session per device/browser
   - Maximum 5 concurrent sessions (configurable)
   - Oldest session auto-deleted when limit exceeded

2. **Session Expiry**:
   - Default: 7 days from creation
   - Extended on token refresh
   - Auto-cleanup via DynamoDB TTL
   - Not renewable after expiry (must re-login)

3. **Device Information Parsing**:
   - Extracted from User-Agent header
   - Format: "{Browser} {Version} on {OS}"
   - Examples:
     - "Chrome 120.0 on Windows 10"
     - "Safari 17.0 on iPhone"
     - "Firefox 119.0 on macOS"

4. **IP Address Handling**:
   - IPv4 or IPv6 format
   - Check X-Forwarded-For header (proxy support)
   - Used for security analytics
   - May be anonymized for GDPR

5. **Location Detection** (Optional):
   - GeoIP lookup based on IP address
   - Format: "City, Country"
   - May be null if lookup fails
   - Not used for access control

6. **Current Session Identification**:
   - Matches refresh token from JWT
   - Marked with `isCurrent: true`
   - Cannot revoke current session via individual revoke
   - Can revoke via logout endpoint

---

## 5. API Endpoints

### 5.1 Authentication Endpoints

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/auth/register` | No | Register new user account |
| POST | `/api/auth/login` | No | Login with email/password |
| POST | `/api/auth/refresh` | No | Refresh access token |
| POST | `/api/auth/verify-email` | No | Verify email with token |
| POST | `/api/auth/logout` | Yes | Logout current session |

### 5.2 Password Management Endpoints

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/password/forgot` | No | Request password reset |
| POST | `/api/password/reset` | No | Reset password with token |
| POST | `/api/password/change` | Yes | Change password (authenticated) |

### 5.3 MFA Endpoints

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| POST | `/api/mfa/setup/totp` | Yes | Setup TOTP authenticator app |
| POST | `/api/mfa/verify` | Yes | Verify and enable MFA |
| POST | `/api/mfa/disable` | Yes | Disable MFA |
| POST | `/api/mfa/verify-login` | No | Verify MFA code during login |

### 5.4 Session Management Endpoints

| Method | Endpoint | Auth Required | Description |
|--------|----------|---------------|-------------|
| GET | `/api/session/active` | Yes | List all active sessions |
| POST | `/api/session/revoke/{sessionId}` | Yes | Revoke specific session |
| POST | `/api/session/revoke-all` | Yes | Revoke all sessions |

### 5.5 Common Headers

**Required Headers**:
```
Content-Type: application/json
X-Tenant-Id: {tenantId}
```

**Authentication Headers** (for protected endpoints):
```
Authorization: Bearer {accessToken}
```

### 5.6 Common Response Codes

| Code | Meaning | Usage |
|------|---------|-------|
| 200 | OK | Successful operation |
| 201 | Created | Resource created successfully |
| 400 | Bad Request | Validation error, invalid input |
| 401 | Unauthorized | Invalid credentials, expired token, account locked |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource already exists (e.g., duplicate email) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Server error |

### 5.7 Error Response Format

All error responses follow this structure:

```json
{
  "success": false,
  "message": "Detailed error message explaining what went wrong",
  "errors": ["Error 1", "Error 2"]  // Optional: for validation errors
}
```

**Examples**:

*Validation Error (400)*:
```json
{
  "success": false,
  "message": "Validation failed",
  "errors": [
    "Password must be at least 8 characters long",
    "Password must contain at least one uppercase letter",
    "Password must contain at least one special character"
  ]
}
```

*Authentication Error (401)*:
```json
{
  "success": false,
  "message": "Invalid email or password"
}
```

*Account Locked (401)*:
```json
{
  "success": false,
  "message": "Account is locked. Please try again in 28 minutes."
}
```

---

## 6. Security Implementation

### 6.1 Password Security

#### 6.1.1 Password Policy

**Requirements**:
- Minimum length: 8 characters (configurable)
- At least 1 uppercase letter (A-Z)
- At least 1 lowercase letter (a-z)
- At least 1 digit (0-9)
- At least 1 special character (!@#$%^&*()_+-=[]{}|;:',.<>?/)

**Configuration**:
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
    }
  }
}
```

#### 6.1.2 Password Hashing

**Algorithm**: BCrypt
- **Work Factor**: 12 (default, configurable)
- **Salt**: Auto-generated unique salt per password
- **Output**: 60-character hash string
- **Security**: Resistant to rainbow table attacks, brute force

**Example Hash**:
```
$2a$12$EixZaYVK1fsbw1Zfbx3OXePaWxn96p36Zdw6TmKFgWLPCQkWXjQrG
 │  │  │                                                    │
 │  │  │                                                    └─ 31-char hash
 │  │  └─ 22-char salt
 │  └─ Work factor (2^12 = 4096 iterations)
 └─ BCrypt identifier
```

#### 6.1.3 Password History

**Purpose**: Prevent password reuse

**Storage**:
- Last 5 password hashes (configurable)
- Comma-separated BCrypt hashes
- Stored in User.PasswordHistory field

**Format**:
```
$2a$12$hash1..., $2a$12$hash2..., $2a$12$hash3..., $2a$12$hash4..., $2a$12$hash5...
```

**Validation**:
- New password compared against each hash in history
- If match found, password rejected
- Oldest hash removed when adding 6th password

### 6.2 Account Lockout

#### 6.2.1 Configuration

```json
{
  "Security": {
    "AccountLockout": {
      "MaxFailedAttempts": 5,
      "LockoutDurationMinutes": 30,
      "EnableLockout": true
    }
  }
}
```

#### 6.2.2 Lockout Flow

```
Login Attempt → Password Incorrect
                      ↓
              Increment FailedLoginAttempts
                      ↓
              FailedLoginAttempts >= 5?
                      ↓
                   Yes → Set LockoutEnd = Now + 30 mins
                      ↓
                   Send Account Locked Email
                      ↓
                   Return 401 Unauthorized
```

#### 6.2.3 Automatic Unlock

**Mechanism**: Time-based
- LockoutEnd timestamp compared on each login attempt
- If LockoutEnd < Now, lockout expired
- User can login normally
- Failed attempts reset to 0 on successful login

**Manual Unlock**: Admin can clear LockoutEnd (future feature)

### 6.3 JWT Token Security

#### 6.3.1 Token Structure

**Access Token** (Short-lived):
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "550e8400-e29b-41d4-a716-446655440000",  // userId
    "email": "user@example.com",
    "role": "Customer",
    "tenantId": "tenant-001",
    "exp": 1730649600,  // 15 minutes from issue
    "iat": 1730648700
  },
  "signature": "..."
}
```

**Refresh Token** (Long-lived):
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "550e8400-e29b-41d4-a716-446655440000",
    "tokenId": "refresh-660f9511-f3ac-52e5-b827-557766551111",
    "exp": 1731254400,  // 7 days from issue
    "iat": 1730649600
  },
  "signature": "..."
}
```

#### 6.3.2 Token Expiry

| Token Type | Expiry | Renewable | Storage |
|------------|--------|-----------|---------|
| Access Token | 15 minutes | Via refresh token | Client memory (not localStorage) |
| Refresh Token | 7 days | No (must re-login) | User entity + UserSession |

#### 6.3.3 Token Validation

**Validation Steps**:
1. Check signature with secret key
2. Verify token not expired (exp claim)
3. Verify issuer matches (iss claim)
4. Verify audience matches (aud claim)
5. Extract user ID and tenant ID
6. Check user still active in database

### 6.4 Multi-Factor Authentication Security

#### 6.4.1 TOTP Security

**Configuration**:
```json
{
  "Security": {
    "Mfa": {
      "TotpIssuer": "Gearify",
      "TotpDigits": 6,
      "TotpPeriod": 30,
      "TotpAlgorithm": "SHA1"
    }
  }
}
```

**Time Drift Tolerance**:
- Accepts codes from current time window
- Also accepts previous window (T-30s)
- Also accepts next window (T+30s)
- Prevents issues with clock skew

**Secret Storage**:
- Base32 encoded 160-bit secret
- Stored encrypted at rest (recommended)
- Never exposed after initial setup

#### 6.4.2 OTP Security

**Code Generation**:
```csharp
var random = new Random();
var code = random.Next(100000, 999999).ToString(); // 6 digits
```

**Code Storage**:
- Hashed with BCrypt before storage
- TTL: 5 minutes (DynamoDB auto-delete)
- Single-use: Marked as IsUsed after verification

**Rate Limiting**:
- Max 3 verification attempts per code
- Exceeding attempts invalidates code
- User must request new code

#### 6.4.3 Backup Codes Security

**Generation**:
```csharp
var code = $"{GenerateRandomString(4)}-{GenerateRandomString(4)}";
// Example: "ABCD-1234"
```

**Storage**:
- BCrypt hashed before storage
- Comma-separated in User.BackupCodes field
- Single-use: Removed from list after use

**Display**:
- Shown only once during MFA setup
- User responsible for secure storage
- Cannot be retrieved later

### 6.5 Session Security

#### 6.5.1 Session Limit

**Configuration**:
```json
{
  "Security": {
    "Session": {
      "MaxConcurrentSessions": 5
    }
  }
}
```

**Enforcement**:
- On new login, count active sessions
- If count >= 5, delete oldest session
- New session created
- Prevents unlimited session accumulation

#### 6.5.2 Session Expiry

**TTL Configuration**:
```json
{
  "Security": {
    "Session": {
      "RefreshTokenExpiryDays": 7
    }
  }
}
```

**DynamoDB TTL**:
- ExpiresAt field used as TTL attribute
- DynamoDB automatically deletes expired sessions
- Cleanup happens within 48 hours of expiry

### 6.6 Email Security

#### 6.6.1 Email Verification Token

**Generation**:
```csharp
var tokenBytes = new byte[32]; // 256 bits
RandomNumberGenerator.Fill(tokenBytes);
var token = Convert.ToBase64String(tokenBytes)
    .Replace("+", "-")
    .Replace("/", "_")
    .Replace("=", ""); // URL-safe
```

**Properties**:
- Length: 43 characters (Base64 URL-safe)
- Entropy: 256 bits
- Expiry: 24 hours
- Single-use: Cleared after verification

#### 6.6.2 Password Reset Token

**Same as Email Verification Token**:
- 32-byte cryptographically secure random
- Base64 URL-safe encoding
- Expiry: 1 hour (shorter for security)
- Single-use: Cleared after password reset

### 6.7 HTTPS/TLS

**Requirements**:
- TLS 1.2 minimum (TLS 1.3 recommended)
- Valid SSL certificate
- HTTPS enforced in production
- HSTS header recommended

**Configuration** (appsettings.Production.json):
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/path/to/certificate.pfx",
          "Password": "certificate-password"
        }
      }
    }
  }
}
```

---

## 7. Email Notifications

### 7.1 Email Templates

All email templates exist in both HTML and plain text formats:

| Template Name | Trigger Event | Recipients |
|---------------|---------------|------------|
| WelcomeEmail | User registration | New user |
| AccountLocked | Account lockout triggered | User |
| AccountUnlocked | Account unlocked (auto/manual) | User |
| PasswordResetRequest | User requests password reset | User |
| PasswordResetSuccess | Password reset completed | User |
| PasswordChanged | Password changed while logged in | User |
| MfaEnabled | MFA enabled successfully | User |
| MfaDisabled | MFA disabled | User |

### 7.2 Email Template Structure

**Location**: `Infrastructure/EmailTemplates/`

**Files**:
- `{TemplateName}.html` - HTML version
- `{TemplateName}.txt` - Plain text version

**Example - WelcomeEmail.html**:
```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <title>Welcome to Gearify</title>
</head>
<body style="font-family: Arial, sans-serif; padding: 20px; background-color: #f4f4f4;">
    <div style="max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 40px;">
        <h1 style="color: #333;">Welcome to Gearify, {{FirstName}}!</h1>
        <p>Thank you for registering. Please verify your email address by clicking the button below:</p>
        <a href="{{VerificationLink}}" style="display: inline-block; background-color: #4CAF50; color: white; padding: 12px 24px; text-decoration: none; border-radius: 4px;">
            Verify Email Address
        </a>
        <p>Or copy and paste this link into your browser:</p>
        <p style="word-break: break-all;">{{VerificationLink}}</p>
        <p>This link will expire in 24 hours.</p>
        <hr>
        <p style="font-size: 12px; color: #999;">
            If you didn't create this account, please ignore this email.
        </p>
    </div>
</body>
</html>
```

### 7.3 Email Template Placeholders

**Common Placeholders**:
- `{{FirstName}}` - User's first name
- `{{LastName}}` - User's last name
- `{{Email}}` - User's email address

**Feature-Specific Placeholders**:

**Welcome Email**:
- `{{VerificationLink}}` - Email verification URL

**Account Locked**:
- `{{FailedAttempts}}` - Number of failed attempts
- `{{LockoutTime}}` - When account was locked
- `{{UnlockTime}}` - When account will unlock

**Password Reset Request**:
- `{{ResetLink}}` - Password reset URL
- `{{ExpiryHours}}` - Token expiry duration (1 hour)

**Password Changed**:
- `{{ChangeTime}}` - When password was changed

**MFA Enabled**:
- `{{Method}}` - MFA method (Totp, Email, Sms)
- `{{SetupTime}}` - When MFA was enabled

### 7.4 Email Service Configuration

**AWS SES Configuration**:
```json
{
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify",
    "ReplyToEmail": "support@gearify.com"
  },
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566"  // LocalStack for development
  },
  "LocalStack": {
    "UseLocalStack": true  // Development only
  }
}
```

**Email Sending Limits (AWS SES)**:
- Sandbox mode: 200 emails/day, verified recipients only
- Production mode: 50,000 emails/day (can request increase)
- Rate limit: 14 emails/second (sandbox), higher in production

---

## 8. Configuration

### 8.1 appsettings.json (Base Configuration)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "JwtSettings": {
    "Secret": "your-256-bit-secret-key-change-in-production",
    "Issuer": "GearifyAuthService",
    "Audience": "GearifyClients",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
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
      "EnableLockout": true
    },
    "Mfa": {
      "CodeExpiryMinutes": 5,
      "MaxVerificationAttempts": 3,
      "BackupCodesCount": 10,
      "TotpIssuer": "Gearify",
      "TotpDigits": 6,
      "TotpPeriod": 30
    },
    "PasswordReset": {
      "TokenExpiryHours": 1,
      "MaxResetAttemptsPerDay": 3
    },
    "Session": {
      "MaxConcurrentSessions": 5,
      "SessionTimeoutMinutes": 60,
      "RefreshTokenExpiryDays": 7
    }
  },
  "Email": {
    "FromEmail": "noreply@gearify.com",
    "FromName": "Gearify"
  },
  "Sms": {
    "Provider": "AWS_SNS",
    "FromNumber": "+1234567890",
    "AwsRegion": "us-east-1"
  },
  "WebAppUrl": "https://app.gearify.com"
}
```

### 8.2 appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  },
  "LocalStack": {
    "UseLocalStack": true,
    "Config": {
      "LocalStackHost": "localhost:4566"
    }
  },
  "AWS": {
    "ServiceURL": "http://localhost:4566",
    "Region": "us-east-1"
  },
  "WebAppUrl": "http://localhost:4200"
}
```

### 8.3 appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Error"
    }
  },
  "LocalStack": {
    "UseLocalStack": false
  },
  "AWS": {
    "Region": "us-east-1"
  },
  "JwtSettings": {
    "Secret": "REPLACE-WITH-PRODUCTION-SECRET-KEY-256-BITS"
  },
  "WebAppUrl": "https://app.gearify.com"
}
```

### 8.4 Environment Variables (Production)

**Recommended for Sensitive Data**:
```bash
# JWT Secret
JWT_SECRET=your-production-secret-key

# AWS Credentials (if not using IAM roles)
AWS_ACCESS_KEY_ID=your-access-key
AWS_SECRET_ACCESS_KEY=your-secret-key
AWS_REGION=us-east-1

# Database
DYNAMODB_TABLE_PREFIX=prod-gearify

# CORS
ALLOWED_ORIGINS=https://app.gearify.com,https://www.gearify.com
```

---

## 9. Deployment Guide

### 9.1 Prerequisites

**Development**:
- .NET 8 SDK
- Docker Desktop
- LocalStack (via Docker Compose)
- IDE (Visual Studio 2022, VS Code, or Rider)

**Production**:
- AWS Account
- DynamoDB tables created
- SES verified domain/email
- SNS configured (for SMS)
- Load balancer (ALB)
- Container registry (ECR)

### 9.2 Local Development Setup

**Step 1: Clone Repository**
```bash
git clone https://github.com/your-org/gearify.git
cd gearify/gearify-auth-svc
```

**Step 2: Start LocalStack**
```bash
cd ../gearify-umbrella
docker compose up -d localstack
```

**Step 3: Verify LocalStack Services**
```bash
# Check health
curl http://localhost:4566/_localstack/health

# List DynamoDB tables
awslocal dynamodb list-tables

# List verified SES emails
awslocal ses list-verified-email-addresses
```

**Step 4: Run Auth Service**
```bash
cd ../gearify-auth-svc
dotnet restore
dotnet run --launch-profile "Local Debug"
```

**Step 5: Access Swagger UI**
```
Open browser: http://localhost:5000/swagger
```

### 9.3 Docker Deployment

**Dockerfile**:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Gearify.AuthService.csproj", "./"]
RUN dotnet restore "Gearify.AuthService.csproj"
COPY . .
RUN dotnet build "Gearify.AuthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Gearify.AuthService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gearify.AuthService.dll"]
```

**Build Image**:
```bash
docker build -t gearify-auth-service:latest .
```

**Run Container**:
```bash
docker run -d \
  -p 8080:80 \
  -e AWS_REGION=us-east-1 \
  -e JWT_SECRET=your-secret-key \
  --name gearify-auth \
  gearify-auth-service:latest
```

### 9.4 AWS Production Deployment

**Step 1: Create DynamoDB Tables**
```bash
# Users table
aws dynamodb create-table \
  --table-name prod-users \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "IndexName=GSI1,KeySchema=[{AttributeName=GSI1PK,KeyType=HASH},{AttributeName=GSI1SK,KeyType=RANGE}],Projection={ProjectionType=ALL},ProvisionedThroughput={ReadCapacityUnits=5,WriteCapacityUnits=5}" \
  --billing-mode PAY_PER_REQUEST

# UserSessions table
aws dynamodb create-table \
  --table-name prod-user-sessions \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --time-to-live-specification \
    Enabled=true,AttributeName=ExpiresAt

# MfaCodes table
aws dynamodb create-table \
  --table-name prod-mfa-codes \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --time-to-live-specification \
    Enabled=true,AttributeName=ExpiresAt
```

**Step 2: Verify SES Domain**
```bash
# Verify domain
aws ses verify-domain-identity --domain gearify.com

# Add DNS records (output from above command)
# Wait for verification
aws ses get-identity-verification-attributes --identities gearify.com
```

**Step 3: Request SES Production Access**
- Go to AWS SES Console
- Request production access
- Provide use case details
- Wait for approval (24-48 hours)

**Step 4: Deploy to ECS/Fargate**
```bash
# Push image to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin {account-id}.dkr.ecr.us-east-1.amazonaws.com
docker tag gearify-auth-service:latest {account-id}.dkr.ecr.us-east-1.amazonaws.com/gearify-auth:latest
docker push {account-id}.dkr.ecr.us-east-1.amazonaws.com/gearify-auth:latest

# Create ECS task definition (JSON file)
# Create ECS service
# Configure ALB target group
```

### 9.5 Monitoring & Alerts

**CloudWatch Metrics**:
- Failed login attempts
- Account lockout events
- Password reset requests
- MFA setup/verification events
- Session creation/revocation

**Recommended Alarms**:
```bash
# High failed login rate
aws cloudwatch put-metric-alarm \
  --alarm-name auth-high-failed-logins \
  --alarm-description "Alert on high failed login rate" \
  --metric-name FailedLogins \
  --namespace GearifyAuth \
  --statistic Sum \
  --period 300 \
  --threshold 100 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 1

# Mass account lockouts
aws cloudwatch put-metric-alarm \
  --alarm-name auth-mass-lockouts \
  --metric-name AccountLockouts \
  --namespace GearifyAuth \
  --statistic Sum \
  --period 300 \
  --threshold 50 \
  --comparison-operator GreaterThanThreshold
```

### 9.6 Backup & Disaster Recovery

**DynamoDB Backups**:
```bash
# Enable point-in-time recovery
aws dynamodb update-continuous-backups \
  --table-name prod-users \
  --point-in-time-recovery-specification PointInTimeRecoveryEnabled=true

# Create on-demand backup
aws dynamodb create-backup \
  --table-name prod-users \
  --backup-name users-backup-$(date +%Y%m%d)
```

**RTO/RPO Targets**:
| Scenario | RTO | RPO | Strategy |
|----------|-----|-----|----------|
| Service crash | < 5 min | 0 | Auto-scaling, health checks |
| Database failure | < 1 hour | < 5 min | DynamoDB auto-failover |
| Region failure | < 4 hours | < 1 hour | Multi-region (future) |
| Data corruption | < 24 hours | < 1 hour | Point-in-time recovery |

---

## 10. Appendix

### 10.1 Security Checklist (Pre-Production)

**Configuration**:
- [ ] Update JWT secret with production key (256-bit minimum)
- [ ] Configure production SES verified domain
- [ ] Set up SNS for SMS (if using SMS MFA)
- [ ] Review all security settings in appsettings.json
- [ ] Update CORS allowed origins
- [ ] Set up rate limiting (API Gateway or middleware)

**AWS Services**:
- [ ] Create DynamoDB tables in production
- [ ] Enable point-in-time recovery on DynamoDB tables
- [ ] Configure DynamoDB auto-scaling
- [ ] Verify SES domain and email addresses
- [ ] Request SES production access
- [ ] Configure SNS for SMS

**Security**:
- [ ] Enable HTTPS/TLS 1.2+ only
- [ ] Configure HSTS headers
- [ ] Review all email templates
- [ ] Test MFA with multiple authenticator apps
- [ ] Test password reset flow end-to-end
- [ ] Verify account lockout timing
- [ ] Test session revocation
- [ ] Audit logging enabled

**Monitoring**:
- [ ] Set up CloudWatch alarms for failed logins
- [ ] Monitor account lockout events
- [ ] Track MFA adoption rates
- [ ] Monitor session creation/revocation rates
- [ ] Set up error rate alerts
- [ ] Configure log aggregation

**Testing**:
- [ ] Load testing completed
- [ ] Security penetration testing
- [ ] API endpoint testing
- [ ] Email delivery testing
- [ ] SMS delivery testing (if applicable)
- [ ] MFA flow testing
- [ ] Password reset flow testing
- [ ] Session management testing

### 10.2 API Testing Examples (cURL)

**Register User**:
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tenant-001" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!",
    "firstName": "Test",
    "lastName": "User"
  }'
```

**Login**:
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: tenant-001" \
  -d '{
    "email": "test@example.com",
    "password": "SecurePass123!"
  }'
```

**Setup TOTP MFA**:
```bash
curl -X POST http://localhost:5000/api/mfa/setup/totp \
  -H "Authorization: Bearer {your-token}" \
  -H "X-Tenant-Id: tenant-001"
```

**Get Active Sessions**:
```bash
curl -X GET http://localhost:5000/api/session/active \
  -H "Authorization: Bearer {your-token}" \
  -H "X-Tenant-Id: tenant-001"
```

### 10.3 Troubleshooting Guide

**Issue: Account Locked Email Not Received**
- Check AWS SES configuration
- Verify email is verified in SES (sandbox mode)
- Check LocalStack logs for SES calls
- Ensure `Email:FromEmail` in appsettings.json is correct

**Issue: MFA QR Code Not Scanning**
- Ensure QR code is properly Base64 decoded
- Try manual entry key instead
- Verify TOTP settings (digits=6, period=30)

**Issue: Password Rejected Despite Meeting Requirements**
- Check if password was used recently (history check)
- Verify all requirements: uppercase, lowercase, digit, special char
- Ensure minimum length is met

**Issue: Session Not Revoked**
- Verify session ID is correct
- Check if user has permission to revoke that session
- Ensure session hasn't already expired

**Issue: SMS OTP Not Received**
- Verify AWS SNS is configured
- Check phone number is in E.164 format (+1234567890)
- For LocalStack, check SNS mock logs
- Verify `Sms:FromNumber` in appsettings.json

### 10.4 Performance Tuning

**Database Optimization**:
- Use DynamoDB auto-scaling or on-demand billing
- Create appropriate GSIs for query patterns
- Enable DynamoDB Accelerator (DAX) for caching (future)

**Caching Strategy**:
- Cache user profile data in Redis after login
- Cache MFA settings
- DO NOT cache tokens or OTP codes

**Rate Limiting** (Recommended):
| Endpoint | Rate Limit | Window | Notes |
|----------|-----------|--------|-------|
| /login | 5 attempts | 15 min | Per IP address |
| /password/forgot | 3 requests | 1 hour | Per email |
| /password/reset | 5 attempts | 1 hour | Per token |
| /mfa/verify | 3 attempts | 5 min | Per code |

### 10.5 Compliance

**GDPR Compliance**:
- User can export their data (future feature)
- User can delete their account (future feature)
- Audit trail of security events
- Data retention policies
- Privacy policy and consent

**OWASP Top 10 Coverage**:
- ✅ A01: Broken Access Control - RBAC, tenant isolation
- ✅ A02: Cryptographic Failures - BCrypt, TLS, secure tokens
- ✅ A03: Injection - Parameterized queries, input validation
- ✅ A04: Insecure Design - Security by design principles
- ✅ A05: Security Misconfiguration - Configuration validation
- ✅ A07: Identification and Authentication Failures - MFA, lockout, password policy

### 10.6 Future Enhancements

**Planned Features**:
1. **Advanced MFA**:
   - WebAuthn/FIDO2 support
   - Biometric authentication
   - Hardware security keys

2. **Security Analytics**:
   - Anomaly detection for login patterns
   - Risk-based authentication
   - Suspicious activity alerts

3. **User Experience**:
   - Remember trusted devices
   - Passwordless authentication (magic links)
   - Social login integration (Google, Microsoft)

4. **Admin Features**:
   - Admin dashboard for user management
   - Manual account unlock
   - Audit log viewer

5. **Compliance**:
   - Data export (GDPR)
   - Account deletion (GDPR)
   - Audit log retention policies

---

## Document Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 2.0 | Nov 2, 2025 | Gearify Dev Team | Complete unified documentation |
| 1.0 | Oct 26, 2025 | Gearify Dev Team | Initial feature documentation |

---

**End of Documentation**

For questions or clarifications, contact: development@gearify.com
