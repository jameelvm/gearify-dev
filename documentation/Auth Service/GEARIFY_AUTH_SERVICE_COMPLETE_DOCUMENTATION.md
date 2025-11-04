# Gearify Authentication Microservice - Complete Documentation

**Document Version**: 2.0
**Last Updated**: November 2, 2025
**Status**: Complete
**Maintained By**: Gearify Development Team

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Architecture](#2-system-architecture)
3. [Domain Entities](#3-domain-entities)
4. [Features & Functionality](#4-features--functionality)
5. [API Endpoints](#5-api-endpoints)
6. [Security Implementation](#6-security-implementation)
7. [Email Notifications](#7-email-notifications)
8. [Configuration](#8-configuration)
9. [Deployment Guide](#9-deployment-guide)
10. [Appendix](#10-appendix)

---

## 1. Executive Summary

### 1.1 Overview

The Gearify Authentication Microservice is an enterprise-grade, security-focused authentication and authorization system built on modern .NET 8 architecture. It provides comprehensive user identity management, multi-factor authentication, session management, and security controls for the Gearify e-commerce platform.

### 1.2 Key Features

- **User Authentication**: Email/password-based authentication with JWT tokens
- **Multi-Factor Authentication (MFA)**: TOTP, Email OTP, SMS OTP, and backup codes
- **Password Security**: Advanced password policies, history tracking, and secure reset flows
- **Account Protection**: Intelligent lockout mechanisms and failed attempt tracking
- **Session Management**: Multi-device session tracking and revocation
- **Email Verification**: Secure email verification with time-limited tokens
- **Multi-Tenancy**: Full tenant isolation and data segregation

### 1.3 Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| **Runtime** | .NET | 8.0 |
| **Framework** | ASP.NET Core | 8.0 |
| **Architecture** | CQRS + Clean Architecture | - |
| **Mediator** | MediatR | Latest |
| **Validation** | FluentValidation | Latest |
| **Database** | AWS DynamoDB | - |
| **Caching** | Redis | Latest |
| **Email** | AWS SES | - |
| **SMS** | AWS SNS | - |
| **Logging** | Serilog | Latest |
| **Security** | BCrypt.Net, OtpNet, QRCoder | Latest |

### 1.4 Compliance & Standards

- **OWASP Top 10**: Addresses all top 10 security vulnerabilities
- **NIST SP 800-63B**: Password guidelines compliance
- **RFC 6238**: TOTP implementation
- **RFC 7519**: JWT implementation
- **GDPR**: Data protection and privacy considerations

---

## 2. System Architecture

### 2.1 High-Level Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        WebApp[Web Application]
        MobileApp[Mobile App]
        AuthApp[Authenticator App<br/>Google/Microsoft/Authy]
    end

    subgraph "API Gateway Layer"
        Gateway[API Gateway]
        TenantMiddleware[Tenant Resolution<br/>Middleware]
        AuthMiddleware[JWT Authentication<br/>Middleware]
    end

    subgraph "Presentation Layer - Controllers"
        AuthController[AuthController<br/>Login, Register, Refresh]
        PasswordController[PasswordController<br/>Forgot, Reset, Change]
        MfaController[MfaController<br/>Setup, Verify, Disable]
        SessionController[SessionController<br/>List, Revoke]
    end

    subgraph "Application Layer - CQRS"
        Commands[Commands<br/>Write Operations]
        Queries[Queries<br/>Read Operations]
        Handlers[Command/Query Handlers]
        Events[Domain Events]
        EventHandlers[Event Handlers]
    end

    subgraph "Domain Layer"
        User[User Entity]
        UserSession[UserSession Entity]
        MfaCode[MfaCode Entity]
        Enums[Enums<br/>MfaMethod]
    end

    subgraph "Application Services"
        PasswordPolicy[Password Policy Service]
        AccountLockout[Account Lockout Service]
        TotpService[TOTP Service]
        OtpService[OTP Service]
        SmsService[SMS Service]
        SessionService[Session Service]
        EmailService[Email Service]
        EmailTemplate[Email Template Service]
        JwtService[JWT Service]
    end

    subgraph "Infrastructure Layer"
        UserRepo[User Repository<br/>DynamoDB]
        SessionRepo[Session Repository<br/>DynamoDB]
        MfaRepo[MFA Code Repository<br/>DynamoDB]
    end

    subgraph "External Services - AWS"
        DynamoDB[(DynamoDB<br/>Users, Sessions, MFA)]
        SES[AWS SES<br/>Email Delivery]
        SNS[AWS SNS<br/>SMS Delivery]
        Redis[(Redis<br/>Caching)]
    end

    subgraph "Development Environment"
        LocalStack[LocalStack<br/>AWS Emulation]
    end

    WebApp --> Gateway
    MobileApp --> Gateway
    AuthApp -.->|Scan QR| MobileApp

    Gateway --> TenantMiddleware
    TenantMiddleware --> AuthMiddleware
    AuthMiddleware --> AuthController
    AuthMiddleware --> PasswordController
    AuthMiddleware --> MfaController
    AuthMiddleware --> SessionController

    AuthController --> Commands
    PasswordController --> Commands
    MfaController --> Commands
    SessionController --> Queries

    Commands --> Handlers
    Queries --> Handlers
    Handlers --> Events
    Events --> EventHandlers

    Handlers --> PasswordPolicy
    Handlers --> AccountLockout
    Handlers --> TotpService
    Handlers --> SessionService

    EventHandlers --> EmailService

    PasswordPolicy --> User
    AccountLockout --> User
    TotpService --> User
    SessionService --> UserSession
    OtpService --> MfaCode

    PasswordPolicy --> UserRepo
    AccountLockout --> UserRepo
    SessionService --> SessionRepo
    OtpService --> MfaRepo

    EmailService --> EmailTemplate
    EmailService --> SES
    SmsService --> SNS

    UserRepo --> DynamoDB
    SessionRepo --> DynamoDB
    MfaRepo --> DynamoDB

    UserRepo --> Redis

    SES -.-> LocalStack
    SNS -.-> LocalStack
    DynamoDB -.-> LocalStack

    style WebApp fill:#e1f5ff
    style MobileApp fill:#e1f5ff
    style Gateway fill:#fff4e6
    style Commands fill:#f3e5f5
    style PasswordPolicy fill:#e8f5e9
    style DynamoDB fill:#c8e6c9
    style SES fill:#ffebee
    style LocalStack fill:#fff9c4
```

### 2.2 Architecture Patterns

#### 2.2.1 Clean Architecture

The service follows Clean Architecture principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                      Presentation Layer                     │
│              (API Controllers, Middleware)                   │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                     Application Layer                       │
│     (Commands, Queries, Handlers, Services Interfaces)      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                       Domain Layer                          │
│              (Entities, Enums, Domain Logic)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                       │
│     (Repositories, External Services, Data Access)          │
└─────────────────────────────────────────────────────────────┘
```

**Benefits**:
- Testability: Business logic independent of frameworks
- Maintainability: Clear separation of concerns
- Flexibility: Easy to swap infrastructure components
- Scalability: Supports horizontal scaling

#### 2.2.2 CQRS Pattern

Commands and Queries are separated for optimal performance and clarity:

**Commands** (Write Operations):
- RegisterUserCommand
- LoginCommand
- ForgotPasswordCommand
- ResetPasswordCommand
- ChangePasswordCommand
- SetupTotpMfaCommand
- VerifyMfaSetupCommand
- DisableMfaCommand
- RevokeSessionCommand

**Queries** (Read Operations):
- GetActiveSessionsQuery

#### 2.2.3 Event-Driven Architecture

Domain events enable loose coupling:

```mermaid
graph LR
    Command[RegisterUserCommand] --> Handler[RegisterUserHandler]
    Handler --> Event[UserCreatedEvent]
    Event --> EmailHandler[SendWelcomeEmailHandler]
    EmailHandler --> SES[AWS SES]
```

### 2.3 Component Responsibility Matrix

| Component | Responsibility | Dependencies |
|-----------|----------------|--------------|
| **AuthController** | Handle authentication requests (login, register, refresh) | LoginHandler, RegisterHandler |
| **PasswordController** | Manage password operations (forgot, reset, change) | ForgotPasswordHandler, ResetPasswordHandler |
| **MfaController** | Manage MFA setup and verification | SetupMfaHandler, VerifyMfaHandler |
| **SessionController** | Session management (list, revoke) | SessionService |
| **PasswordPolicyService** | Validate passwords, manage history, BCrypt hashing | IUserRepository |
| **AccountLockoutService** | Track failed attempts, manage lockouts | IUserRepository, IEmailService |
| **TotpService** | Generate/verify TOTP codes, QR codes | OtpNet, QRCoder |
| **OtpService** | Generate/verify email/SMS OTP codes | IMfaCodeRepository |
| **SmsService** | Send SMS via AWS SNS | AWS SNS SDK |
| **SessionService** | Create, track, revoke sessions | IUserSessionRepository |
| **EmailService** | Send emails via AWS SES | AWS SES SDK, IEmailTemplateService |
| **EmailTemplateService** | Load and render HTML email templates | File System |
| **JwtService** | Generate and validate JWT tokens | JWT Library |
| **UserRepository** | User CRUD operations | DynamoDB, Redis |
| **SessionRepository** | Session CRUD operations | DynamoDB |
| **MfaCodeRepository** | MFA code CRUD operations | DynamoDB |

### 2.4 Data Flow Architecture

#### 2.4.1 Authentication Flow

```mermaid
sequenceDiagram
    participant Client
    participant Gateway
    participant AuthController
    participant LoginHandler
    participant AccountLockout
    participant PasswordPolicy
    participant UserRepo
    participant JwtService
    participant SessionService

    Client->>Gateway: POST /api/auth/login
    Gateway->>AuthController: LoginCommand
    AuthController->>LoginHandler: Handle(command)

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    UserRepo-->>LoginHandler: User

    LoginHandler->>AccountLockout: IsLockedOut(user)

    alt Account Locked
        AccountLockout-->>LoginHandler: true
        LoginHandler-->>Client: 401 Account Locked
    else Not Locked
        LoginHandler->>PasswordPolicy: VerifyPassword(password, hash)

        alt Invalid Password
            PasswordPolicy-->>LoginHandler: false
            LoginHandler->>AccountLockout: RecordFailedAttempt(user)
            LoginHandler-->>Client: 401 Invalid Credentials
        else Valid Password
            PasswordPolicy-->>LoginHandler: true
            LoginHandler->>AccountLockout: ResetFailedAttempts(user)
            LoginHandler->>JwtService: GenerateTokens(user)
            JwtService-->>LoginHandler: tokens
            LoginHandler->>SessionService: CreateSession(user, token)
            LoginHandler-->>Client: 200 OK + tokens
        end
    end
```

#### 2.4.2 Registration Flow

```mermaid
sequenceDiagram
    participant Client
    participant Gateway
    participant AuthController
    participant RegisterHandler
    participant PasswordPolicy
    participant UserRepo
    participant EventBus
    participant EmailHandler
    participant EmailService

    Client->>Gateway: POST /api/auth/register
    Gateway->>AuthController: RegisterUserCommand
    AuthController->>RegisterHandler: Handle(command)

    RegisterHandler->>PasswordPolicy: ValidatePassword(password)

    alt Password Invalid
        PasswordPolicy-->>RegisterHandler: ValidationResult(false)
        RegisterHandler-->>Client: 400 Validation Errors
    else Password Valid
        RegisterHandler->>UserRepo: GetByEmailAsync(email)

        alt User Exists
            UserRepo-->>RegisterHandler: User
            RegisterHandler-->>Client: 400 Email Already Exists
        else User Not Exists
            RegisterHandler->>PasswordPolicy: HashPassword(password)
            PasswordPolicy-->>RegisterHandler: passwordHash

            RegisterHandler->>UserRepo: CreateAsync(user)
            UserRepo-->>RegisterHandler: Created User

            RegisterHandler->>EventBus: Publish(UserCreatedEvent)
            EventBus->>EmailHandler: Handle(UserCreatedEvent)
            EmailHandler->>EmailService: SendWelcomeEmail(user)

            RegisterHandler-->>Client: 200 OK + tokens
        end
    end
```

### 2.5 Security Layers

The service implements multiple security layers:

```
┌─────────────────────────────────────────────────────────────┐
│  Layer 1: Transport Security (HTTPS/TLS 1.2+)              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 2: API Gateway (CORS, Rate Limiting)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 3: Authentication (JWT Bearer Tokens)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 4: Authorization (RBAC, Tenant Isolation)           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 5: Password Security (BCrypt, Policy, History)      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 6: Account Protection (Lockout, MFA)                │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│  Layer 7: Audit & Monitoring (Logging, Metrics)            │
└─────────────────────────────────────────────────────────────┘
```

### 2.6 Deployment Architecture

#### 2.6.1 Development Environment

```mermaid
graph TB
    Dev[Developer Machine] --> Docker[Docker Desktop]
    Docker --> LocalStack[LocalStack Container<br/>DynamoDB, SES, SNS]
    Docker --> AuthService[Auth Service Container]
    Docker --> Redis[Redis Container]

    AuthService --> LocalStack
    AuthService --> Redis

    style Dev fill:#e1f5ff
    style LocalStack fill:#e8f5e9
    style AuthService fill:#f3e5f5
```

#### 2.6.2 Production Environment

```mermaid
graph TB
    Internet[Internet/Users] --> LB[Application Load Balancer]
    LB --> ASG[Auto Scaling Group]

    ASG --> Auth1[Auth Service Instance 1]
    ASG --> Auth2[Auth Service Instance 2]
    ASG --> Auth3[Auth Service Instance 3]

    Auth1 --> DDB[(DynamoDB)]
    Auth2 --> DDB
    Auth3 --> DDB

    Auth1 --> Redis[(Redis Cluster)]
    Auth2 --> Redis
    Auth3 --> Redis

    Auth1 --> SES[AWS SES]
    Auth2 --> SES
    Auth3 --> SES

    Auth1 --> SNS[AWS SNS]
    Auth2 --> SNS
    Auth3 --> SNS

    DDB --> Backup[Point-in-Time Recovery]

    CloudWatch[CloudWatch] --> Auth1
    CloudWatch --> Auth2
    CloudWatch --> Auth3

    style Internet fill:#e1f5ff
    style LB fill:#fff4e6
    style ASG fill:#fff9c4
    style DDB fill:#e8f5e9
```

---

## 3. Domain Entities

### 3.1 User Entity

**Location**: `Domain/Entities/User.cs`

**Purpose**: The User entity represents an authenticated user in the Gearify system. It contains all authentication, security, profile, and session-related information.

#### 3.1.1 Entity Schema

| Property | Type | Default | Description | Used By |
|----------|------|---------|-------------|---------|
| **Core Identity** |||||
| `Id` | string | GUID | Unique user identifier | All features |
| `TenantId` | string | - | Multi-tenant identifier for data isolation | All features |
| `Email` | string | - | User's email address (lowercase, unique per tenant) | Login, Password Reset, MFA |
| `PasswordHash` | string | - | BCrypt hashed password (work factor: default) | Login, Password Policy |
| **Profile Information** |||||
| `FirstName` | string | - | User's first name | Email templates, UI |
| `LastName` | string | - | User's last name | Email templates, UI |
| `Phone` | string | - | User's phone number (E.164 format: +1234567890) | SMS MFA |
| `Role` | string | "Customer" | User role (Admin, Customer, Manager) | Authorization, RBAC |
| **Audit Timestamps** |||||
| `CreatedAt` | DateTime | UtcNow | Account creation timestamp | Audit, Analytics |
| `UpdatedAt` | DateTime | UtcNow | Last modification timestamp | Audit |
| `LastLoginAt` | DateTime? | null | Last successful login timestamp | Analytics, Security monitoring |
| **Account Status** |||||
| `IsActive` | bool | true | Whether account is active (soft delete) | Login, Authorization |
| `EmailVerified` | bool | false | Whether email has been verified | Registration flow |
| **Token Management** |||||
| `RefreshToken` | string? | null | Current active refresh token (JWT) | Token refresh flow |
| `RefreshTokenExpiry` | DateTime? | null | Refresh token expiration time | Token refresh flow |
| `EmailVerificationToken` | string? | null | Email verification token | Registration flow |
| `EmailVerificationTokenExpiry` | DateTime? | null | Email verification token expiration (24 hours) | Registration flow |
| **Multi-Factor Authentication** |||||
| `MfaEnabled` | bool | false | Whether MFA is enabled for this user | Login, MFA flows |
| `PreferredMfaMethod` | string | "None" | Preferred MFA method (None, Totp, Email, Sms) | Login, MFA flows |
| `TotpSecret` | string? | null | TOTP secret for authenticator apps (Base32 encoded) | TOTP MFA |
| `BackupCodes` | string? | null | Comma-separated hashed backup codes (10 codes) | MFA recovery |
| `LastMfaSetupAt` | DateTime? | null | When MFA was last configured | Audit, Security monitoring |
| **Password Management** |||||
| `PasswordResetToken` | string? | null | Password reset token (cryptographically secure) | Password reset flow |
| `PasswordResetTokenExpiry` | DateTime? | null | Token expiration (1 hour default) | Password reset flow |
| `LastPasswordChangeAt` | DateTime? | null | When password was last changed | Security monitoring, Compliance |
| `PasswordHistory` | string? | null | Comma-separated last 5 hashed passwords | Password policy |
| **Account Security** |||||
| `FailedLoginAttempts` | int | 0 | Count of consecutive failed login attempts | Account lockout |
| `LockoutEnd` | DateTime? | null | When account lockout ends (30 min default) | Account lockout |
| `LockoutEnabled` | bool | true | Whether lockout feature is enabled for user | Account lockout |
| **Session Tracking** |||||
| `ActiveSessionCount` | int | 0 | Current number of active sessions | Session management |

#### 3.1.2 Property Details & Business Rules

##### Core Identity Properties

**Id (string)**
- **Format**: GUID (e.g., "550e8400-e29b-41d4-a716-446655440000")
- **Generation**: Auto-generated on entity creation
- **Uniqueness**: Globally unique
- **Immutable**: Never changes after creation
- **Usage**: Primary key in database, user references in other entities

**TenantId (string)**
- **Purpose**: Enables multi-tenancy data isolation
- **Format**: String identifier (e.g., "tenant-001", "acme-corp")
- **Business Rule**: All queries must filter by TenantId
- **Security**: Prevents data leakage between tenants
- **Database Schema**: Part of partition key (PK: TENANT#{tenantId}#USER#{userId})

**Email (string)**
- **Format**: Valid email address, stored in lowercase
- **Uniqueness**: Must be unique within a tenant
- **Validation**: RFC 5322 compliant email validation
- **Business Rule**: Used as primary login identifier
- **Case Insensitive**: Always stored and compared in lowercase
- **Maximum Length**: 255 characters

**PasswordHash (string)**
- **Hashing Algorithm**: BCrypt
- **Work Factor**: Configurable (default: 12)
- **Salt**: Auto-generated unique salt per password
- **Format**: BCrypt hash string (60 characters)
- **Security**: Never stored in plain text, never logged
- **Verification**: Use BCrypt.Verify() method only

##### MFA Properties

**MfaEnabled (bool)**
- **Purpose**: Master switch for MFA requirement
- **Default**: false (MFA is opt-in)
- **Business Rule**: If true, user must complete MFA during login
- **Security**: Cannot be disabled without password verification
- **Audit**: Changes are logged and email notifications sent

**PreferredMfaMethod (string)**
- **Values**: "None", "Totp", "Email", "Sms"
- **Purpose**: Determines which MFA method to use during login
- **Default**: "None"
- **Business Rule**: Must have MfaEnabled=true to use methods other than "None"
- **Validation**: Must be one of the enum values

**TotpSecret (string?)**
- **Purpose**: Secret key for TOTP (Time-based One-Time Password) generation
- **Format**: Base32 encoded string
- **Length**: 32 characters (160 bits of entropy)
- **Generation**: Cryptographically secure random generation
- **Security**: Encrypted at rest (recommended), never exposed to client except during setup
- **Compatibility**: Works with Google Authenticator, Microsoft Authenticator, Authy, etc.
- **Standard**: RFC 6238 compliant

**BackupCodes (string?)**
- **Purpose**: Emergency access codes when primary MFA method unavailable
- **Format**: Comma-separated hashed codes
- **Count**: 10 codes by default
- **Code Format**: 8 alphanumeric characters (e.g., "ABCD-1234")
- **Security**: BCrypt hashed before storage
- **Usage**: Single-use only, marked as used after first use
- **Display**: Shown only once during MFA setup

##### Password Security Properties

**PasswordResetToken (string?)**
- **Purpose**: Secure token for password reset verification
- **Generation**: Cryptographically secure random bytes (32 bytes), Base64 encoded
- **Expiry**: 1 hour (configurable)
- **Single Use**: Invalidated after successful password reset
- **Security**: Never sent in plain text, only in secure reset link
- **Format**: URL-safe Base64 string

**PasswordHistory (string?)**
- **Purpose**: Prevent password reuse
- **Format**: Comma-separated BCrypt hashes
- **Count**: Last 5 passwords (configurable)
- **Validation**: New password cannot match any in history
- **Storage**: Full BCrypt hashes (same as current password)
- **Rotation**: Oldest hash removed when limit exceeded

##### Account Lockout Properties

**FailedLoginAttempts (int)**
- **Purpose**: Track consecutive failed login attempts
- **Range**: 0 to MaxFailedAttempts (default: 5)
- **Reset Conditions**:
  - Successful login
  - Password reset completion
  - Manual unlock by admin
- **Increment**: Each failed login attempt
- **Lockout Trigger**: Equals MaxFailedAttempts

**LockoutEnd (DateTime?)**
- **Purpose**: Timestamp when account lockout expires
- **Calculation**: Current time + LockoutDurationMinutes
- **Default Duration**: 30 minutes
- **Null State**: No active lockout
- **Automatic Unlock**: System checks this timestamp on login attempt
- **Time Zone**: Always stored in UTC

#### 3.1.3 DynamoDB Schema

**Table Name**: `Users`

**Primary Key**:
- **PK (Partition Key)**: `TENANT#{tenantId}#USER#{userId}`
- **SK (Sort Key)**: `PROFILE`

**Global Secondary Indexes**:

1. **GSI1 - Email Lookup**
   - **PK**: `TENANT#{tenantId}#EMAIL#{email}`
   - **SK**: `USER#{userId}`
   - **Purpose**: Fast user lookup by email for login

2. **GSI2 - Role-based Queries**
   - **PK**: `TENANT#{tenantId}#ROLE#{role}`
   - **SK**: `USER#{userId}`
   - **Purpose**: List users by role

**Attributes**: All User entity properties are stored as attributes

**TTL Field**: Not applicable (users don't auto-expire)

### 3.2 UserSession Entity

**Location**: `Domain/Entities/UserSession.cs`

**Purpose**: Tracks individual user sessions across multiple devices for security auditing and session management.

#### 3.2.1 Entity Schema

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | string | GUID | Unique session identifier |
| `UserId` | string | - | Reference to User.Id |
| `TenantId` | string | - | Tenant identifier (for isolation) |
| `RefreshToken` | string | - | JWT refresh token for this session |
| `DeviceInfo` | string | - | User agent string (browser/OS/device info) |
| `IpAddress` | string | - | IP address of the session |
| `Location` | string? | null | Geographic location (city, country) - optional |
| `CreatedAt` | DateTime | UtcNow | Session creation timestamp |
| `LastAccessedAt` | DateTime | UtcNow | Last activity timestamp |
| `ExpiresAt` | DateTime | - | Session expiration timestamp |
| `IsActive` | bool | true | Whether session is currently active |

#### 3.2.2 Property Details

**DeviceInfo (string)**
- **Source**: User-Agent header from HTTP request
- **Format**: "Chrome 120.0 on Windows 10", "Safari 17.0 on iPhone"
- **Parsing**: Extract browser name, version, OS
- **Purpose**: Help users identify their devices
- **Example**: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"

**IpAddress (string)**
- **Format**: IPv4 (e.g., "192.168.1.100") or IPv6
- **Source**: Request IP address from headers
- **Proxy Handling**: Check X-Forwarded-For header
- **Privacy**: May be anonymized for GDPR compliance
- **Usage**: Detect suspicious login patterns

**ExpiresAt (DateTime)**
- **Calculation**: CreatedAt + RefreshTokenExpiryDays (default: 7 days)
- **Auto Cleanup**: Sessions automatically invalid after expiry
- **Renewal**: Updated on token refresh
- **Time Zone**: UTC

#### 3.2.3 DynamoDB Schema

**Table Name**: `UserSessions`

**Primary Key**:
- **PK**: `USER#{userId}`
- **SK**: `SESSION#{sessionId}`

**Purpose**: All sessions for a user can be queried efficiently

**Attributes**: All UserSession properties

**TTL Field**: `ExpiresAt` (auto-delete expired sessions)

### 3.3 MfaCode Entity

**Location**: `Domain/Entities/MfaCode.cs`

**Purpose**: Stores temporary OTP codes for Email/SMS MFA methods.

#### 3.3.1 Entity Schema

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Id` | string | GUID | Unique code identifier |
| `UserId` | string | - | Reference to User.Id |
| `TenantId` | string | - | Tenant identifier |
| `CodeHash` | string | - | BCrypt hashed OTP code |
| `Method` | string | - | "Email" or "Sms" (stored as string) |
| `CreatedAt` | DateTime | UtcNow | Code generation timestamp |
| `ExpiresAt` | DateTime | - | Code expiration (default: 5 minutes) |
| `IsUsed` | bool | false | Whether code has been consumed |
| `AttemptCount` | int | 0 | Number of verification attempts |
| `Purpose` | string | - | "Login", "Setup", "Verification" |

#### 3.3.2 Property Details

**CodeHash (string)**
- **Original Code**: 6-digit numeric (e.g., "123456")
- **Hashing**: BCrypt hashed before storage
- **Why Hash**: Prevents database compromise from revealing valid codes
- **Verification**: Use BCrypt.Verify() with user input

**AttemptCount (int)**
- **Purpose**: Rate limiting for brute force protection
- **Maximum**: 3 attempts (configurable)
- **Action on Exceed**: Code becomes invalid, user must request new code
- **Reset**: Never (must request new code)

**Purpose (string)**
- **Values**: "Login", "Setup", "Verification"
- **Usage**: Different code types for different flows
- **Audit**: Track what codes are being used for

#### 3.3.3 DynamoDB Schema

**Table Name**: `MfaCodes`

**Primary Key**:
- **PK**: `USER#{userId}`
- **SK**: `MFACODE#{codeId}`

**Purpose**: Efficient cleanup of user's old codes

**TTL Field**: `ExpiresAt` (auto-delete after 5 minutes)

### 3.4 Enums

#### 3.4.1 MfaMethod Enum

**Location**: `Domain/Enums/MfaMethod.cs`

```csharp
public enum MfaMethod
{
    None = 0,   // No MFA enabled
    Totp = 1,   // Time-based One-Time Password (Authenticator apps)
    Email = 2,  // Email-based OTP
    Sms = 3     // SMS-based OTP
}
```

**Usage in User Entity**: Stored as string representation
- **Storage**: "None", "Totp", "Email", "Sms"
- **Conversion**: Enum.Parse<MfaMethod>(user.PreferredMfaMethod)
- **Validation**: Must be valid enum value

---

## 4. Features & Functionality

### 4.1 User Registration

**Endpoint**: `POST /api/auth/register`

**Purpose**: Create a new user account with email verification.

#### 4.1.1 Feature Overview

User registration is the entry point for new users to join the Gearify platform. The process includes:
- Password policy validation
- Email uniqueness check
- Secure password hashing
- Email verification token generation
- Welcome email dispatch
- Initial JWT token generation

#### 4.1.2 Sequence Diagram

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant RegisterHandler
    participant PasswordPolicy
    participant UserRepo
    participant DynamoDB
    participant EventBus
    participant EmailHandler
    participant EmailTemplate
    participant SES

    Client->>API: POST /api/auth/register<br/>{email, password, firstName, lastName}
    API->>RegisterHandler: RegisterUserCommand

    Note over RegisterHandler: Step 1: Validate Password
    RegisterHandler->>PasswordPolicy: ValidatePassword(password)
    PasswordPolicy->>PasswordPolicy: Check length >= 8
    PasswordPolicy->>PasswordPolicy: Check uppercase
    PasswordPolicy->>PasswordPolicy: Check lowercase
    PasswordPolicy->>PasswordPolicy: Check digit
    PasswordPolicy->>PasswordPolicy: Check special char

    alt Password Invalid
        PasswordPolicy-->>RegisterHandler: ValidationResult(false, errors[])
        RegisterHandler-->>Client: 400 Bad Request<br/>{errors: [...]}
    end

    PasswordPolicy-->>RegisterHandler: ValidationResult(true)

    Note over RegisterHandler: Step 2: Check Email Uniqueness
    RegisterHandler->>UserRepo: GetByEmailAsync(email, tenantId)
    UserRepo->>DynamoDB: Query GSI1 (Email index)

    alt User Already Exists
        DynamoDB-->>UserRepo: User found
        UserRepo-->>RegisterHandler: Existing User
        RegisterHandler-->>Client: 400 Bad Request<br/>{error: "Email already registered"}
    end

    DynamoDB-->>UserRepo: User not found
    UserRepo-->>RegisterHandler: null

    Note over RegisterHandler: Step 3: Hash Password
    RegisterHandler->>PasswordPolicy: HashPassword(password)
    PasswordPolicy->>PasswordPolicy: Generate salt
    PasswordPolicy->>PasswordPolicy: BCrypt.Hash(password, salt)
    PasswordPolicy-->>RegisterHandler: passwordHash

    Note over RegisterHandler: Step 4: Generate Verification Token
    RegisterHandler->>RegisterHandler: GenerateSecureToken()
    RegisterHandler->>RegisterHandler: Set token expiry (24 hours)

    Note over RegisterHandler: Step 5: Create User
    RegisterHandler->>RegisterHandler: Initialize password history
    RegisterHandler->>UserRepo: CreateAsync(newUser)
    UserRepo->>DynamoDB: PutItem
    DynamoDB-->>UserRepo: Success
    UserRepo-->>RegisterHandler: Created User

    Note over RegisterHandler: Step 6: Publish Event
    RegisterHandler->>EventBus: Publish(UserCreatedEvent)

    Note over RegisterHandler: Step 7: Generate JWT
    RegisterHandler->>RegisterHandler: Generate Access Token
    RegisterHandler->>RegisterHandler: Generate Refresh Token
    RegisterHandler-->>Client: 200 OK<br/>{token, refreshToken, user}

    Note over EventBus: Asynchronous Email Flow
    EventBus->>EmailHandler: Handle(UserCreatedEvent)
    EmailHandler->>EmailTemplate: RenderTemplateAsync("WelcomeEmail")
    EmailTemplate->>EmailTemplate: Load WelcomeEmail.html
    EmailTemplate->>EmailTemplate: Replace {{FirstName}}
    EmailTemplate->>EmailTemplate: Replace {{VerificationLink}}
    EmailTemplate-->>EmailHandler: Rendered HTML

    EmailHandler->>SES: SendEmailAsync(to, subject, body)
    SES->>SES: Queue email
    SES-->>EmailHandler: MessageId
    EmailHandler-->>EventBus: Email sent successfully
```

#### 4.1.3 Request/Response

**Request**:
```json
POST /api/auth/register
Content-Type: application/json
X-Tenant-Id: tenant-001

{
  "email": "john.doe@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Success Response (200 OK)**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "emailVerified": false,
    "role": "Customer"
  }
}
```

**Error Response - Weak Password (400 Bad Request)**:
```json
{
  "success": false,
  "message": "Password must be at least 8 characters long. Password must contain at least one uppercase letter. Password must contain at least one special character."
}
```

**Error Response - Email Exists (400 Bad Request)**:
```json
{
  "success": false,
  "message": "A user with this email already exists."
}
```

#### 4.1.4 Business Rules

1. **Password Policy Enforcement**:
   - Minimum 8 characters
   - At least 1 uppercase letter (A-Z)
   - At least 1 lowercase letter (a-z)
   - At least 1 digit (0-9)
   - At least 1 special character (!@#$%^&*()_+-=[]{}|;:,.<>?)

2. **Email Uniqueness**:
   - Email must be unique within the tenant
   - Case-insensitive comparison
   - Stored in lowercase

3. **Email Verification**:
   - Verification token valid for 24 hours
   - Cryptographically secure random token (32 bytes)
   - URL-safe Base64 encoding

4. **Initial User State**:
   - `IsActive`: true
   - `EmailVerified`: false
   - `MfaEnabled`: false
   - `Role`: "Customer"
   - `FailedLoginAttempts`: 0

5. **Password History**:
   - First password hash added to history
   - Prevents reuse in future password changes

6. **JWT Tokens**:
   - Access token expiry: 15 minutes (configurable)
   - Refresh token expiry: 7 days (configurable)
   - Tokens contain: userId, email, role, tenantId

---

### 4.2 User Login (Sign In)

**Endpoint**: `POST /api/auth/login`

**Purpose**: Authenticate a user and issue JWT tokens for API access.

#### 4.2.1 Feature Overview

The login feature handles user authentication with the following security measures:
- Email/password verification
- Account lockout protection
- Failed attempt tracking
- MFA verification (if enabled)
- Session creation and tracking
- JWT token generation

#### 4.2.2 Sequence Diagram - Standard Login (No MFA)

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant LoginHandler
    participant UserRepo
    participant AccountLockout
    participant PasswordPolicy
    participant JwtService
    participant SessionService
    participant SessionRepo
    participant DynamoDB

    Client->>API: POST /api/auth/login<br/>{email, password}
    API->>LoginHandler: LoginCommand

    Note over LoginHandler: Step 1: Find User
    LoginHandler->>UserRepo: GetByEmailAsync(email, tenantId)
    UserRepo->>DynamoDB: Query GSI1 (Email index)

    alt User Not Found
        DynamoDB-->>UserRepo: Not found
        UserRepo-->>LoginHandler: null
        LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid email or password"}
    end

    DynamoDB-->>UserRepo: User found
    UserRepo-->>LoginHandler: User

    Note over LoginHandler: Step 2: Check Account Active
    alt Account Inactive
        LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Account is inactive"}
    end

    Note over LoginHandler: Step 3: Check Account Lockout
    LoginHandler->>AccountLockout: IsLockedOut(user)
    AccountLockout->>AccountLockout: Check LockoutEnd > Now

    alt Account Locked
        AccountLockout-->>LoginHandler: true (remaining time)
        LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Account locked. Try again in X mins"}
    end

    AccountLockout-->>LoginHandler: false (not locked)

    Note over LoginHandler: Step 4: Verify Password
    LoginHandler->>PasswordPolicy: VerifyPassword(password, user.PasswordHash)
    PasswordPolicy->>PasswordPolicy: BCrypt.Verify(password, hash)

    alt Password Invalid
        PasswordPolicy-->>LoginHandler: false

        Note over LoginHandler: Record Failed Attempt
        LoginHandler->>AccountLockout: RecordFailedLoginAttempt(user)
        AccountLockout->>AccountLockout: Increment FailedLoginAttempts
        AccountLockout->>AccountLockout: Check if >= MaxFailedAttempts

        alt Max Attempts Reached
            AccountLockout->>AccountLockout: Set LockoutEnd = Now + 30 mins
            AccountLockout->>UserRepo: UpdateAsync(user)
            UserRepo->>DynamoDB: UpdateItem
            AccountLockout->>AccountLockout: Send account locked email
            AccountLockout-->>LoginHandler: Account now locked
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Account locked"}
        else Not Yet Locked
            AccountLockout->>UserRepo: UpdateAsync(user)
            UserRepo->>DynamoDB: UpdateItem
            AccountLockout-->>LoginHandler: Attempt recorded
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid email or password"}
        end
    end

    PasswordPolicy-->>LoginHandler: true (password valid)

    Note over LoginHandler: Step 5: Reset Failed Attempts
    LoginHandler->>AccountLockout: ResetFailedLoginAttempts(user)
    AccountLockout->>AccountLockout: Set FailedLoginAttempts = 0
    AccountLockout->>AccountLockout: Clear LockoutEnd
    AccountLockout->>UserRepo: UpdateAsync(user)

    Note over LoginHandler: Step 6: Update Last Login
    LoginHandler->>LoginHandler: Set LastLoginAt = Now

    Note over LoginHandler: Step 7: Generate Tokens
    LoginHandler->>JwtService: GenerateAccessToken(user)
    JwtService->>JwtService: Create claims (id, email, role, tenant)
    JwtService->>JwtService: Sign with secret key
    JwtService-->>LoginHandler: accessToken

    LoginHandler->>JwtService: GenerateRefreshToken()
    JwtService->>JwtService: Generate secure random token
    JwtService-->>LoginHandler: refreshToken

    LoginHandler->>LoginHandler: Set RefreshTokenExpiry = Now + 7 days

    Note over LoginHandler: Step 8: Create Session
    LoginHandler->>SessionService: CreateSessionAsync(user, refreshToken, deviceInfo, ip)
    SessionService->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo->>DynamoDB: Query by PK (USER#{userId})
    DynamoDB-->>SessionRepo: Active sessions

    alt Session Limit Exceeded (> 5)
        SessionService->>SessionService: Get oldest session
        SessionService->>SessionRepo: DeleteAsync(oldestSession)
        SessionRepo->>DynamoDB: DeleteItem
    end

    SessionService->>SessionRepo: CreateAsync(newSession)
    SessionRepo->>DynamoDB: PutItem
    SessionService-->>LoginHandler: Session created

    Note over LoginHandler: Step 9: Save User Updates
    LoginHandler->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem

    LoginHandler-->>Client: 200 OK<br/>{token, refreshToken, user, requiresMfa: false}
```

#### 4.2.3 Sequence Diagram - Login with MFA

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant LoginHandler
    participant UserRepo
    participant PasswordPolicy
    participant OtpService
    participant MfaRepo
    participant EmailService
    participant SES

    Note over Client,SES: Phase 1: Initial Login (Username/Password)

    Client->>API: POST /api/auth/login<br/>{email, password}
    API->>LoginHandler: LoginCommand

    LoginHandler->>UserRepo: GetByEmailAsync(email)
    UserRepo-->>LoginHandler: User

    LoginHandler->>PasswordPolicy: VerifyPassword(password, hash)
    PasswordPolicy-->>LoginHandler: true (valid)

    Note over LoginHandler: Check if MFA enabled
    LoginHandler->>LoginHandler: Check user.MfaEnabled == true

    alt MFA Enabled - TOTP Method
        Note over LoginHandler: User has authenticator app configured
        LoginHandler-->>Client: 200 OK<br/>{requiresMfa: true, method: "Totp", userId}

        Note over Client: User opens authenticator app
        Note over Client: User enters 6-digit code

        Client->>API: POST /api/mfa/verify-login<br/>{userId, code}
        API->>LoginHandler: VerifyMfaLoginCommand

        LoginHandler->>UserRepo: GetByIdAsync(userId)
        UserRepo-->>LoginHandler: User

        LoginHandler->>LoginHandler: Verify TOTP code
        LoginHandler->>LoginHandler: Check code validity (30-sec window)

        alt TOTP Code Invalid
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid code"}
        end

        LoginHandler->>LoginHandler: Generate JWT tokens
        LoginHandler-->>Client: 200 OK<br/>{token, refreshToken, user}

    else MFA Enabled - Email Method
        Note over LoginHandler: Generate OTP code
        LoginHandler->>OtpService: GenerateCode()
        OtpService->>OtpService: Generate 6-digit code
        OtpService-->>LoginHandler: "123456"

        Note over LoginHandler: Store OTP code
        LoginHandler->>OtpService: StoreCodeAsync(userId, code, Email, Login, 5mins)
        OtpService->>OtpService: Hash code with BCrypt
        OtpService->>MfaRepo: CreateAsync(MfaCode)
        MfaRepo-->>OtpService: Stored

        Note over LoginHandler: Send OTP via email
        LoginHandler->>EmailService: SendOtpEmailAsync(user, code)
        EmailService->>SES: SendEmailAsync
        SES-->>EmailService: MessageId

        LoginHandler-->>Client: 200 OK<br/>{requiresMfa: true, method: "Email", userId}

        Note over Client: User checks email
        Note over Client: User enters code from email

        Client->>API: POST /api/mfa/verify-login<br/>{userId, code}
        API->>LoginHandler: VerifyMfaLoginCommand

        LoginHandler->>OtpService: VerifyCodeAsync(userId, code, Login)
        OtpService->>MfaRepo: GetByUserAsync(userId)
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

        OtpService->>OtpService: BCrypt.Verify(code, codeHash)

        alt Code Invalid
            OtpService->>OtpService: Increment AttemptCount
            OtpService->>MfaRepo: UpdateAsync(mfaCode)
            OtpService-->>LoginHandler: false
            LoginHandler-->>Client: 401 Unauthorized<br/>{error: "Invalid code"}
        end

        OtpService->>OtpService: Mark code as used
        OtpService->>MfaRepo: UpdateAsync(IsUsed = true)
        OtpService-->>LoginHandler: true

        LoginHandler->>LoginHandler: Generate JWT tokens
        LoginHandler-->>Client: 200 OK<br/>{token, refreshToken, user}
    end
```

#### 4.2.4 Request/Response

**Request - Standard Login**:
```json
POST /api/auth/login
Content-Type: application/json
X-Tenant-Id: tenant-001

{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

**Success Response - No MFA (200 OK)**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "emailVerified": true,
    "mfaEnabled": false
  },
  "requiresMfa": false
}
```

**Success Response - MFA Required (200 OK)**:
```json
{
  "requiresMfa": true,
  "method": "Totp",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Please enter the code from your authenticator app."
}
```

**Error Responses**:

*Invalid Credentials (401)*:
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

*Account Inactive (401)*:
```json
{
  "success": false,
  "message": "Your account is inactive. Please contact support."
}
```

#### 4.2.5 Business Rules

1. **Email Lookup**:
   - Case-insensitive email comparison
   - Scoped to tenant (multi-tenancy)

2. **Password Verification**:
   - BCrypt comparison (secure timing)
   - Never reveals which field (email/password) was incorrect

3. **Account Lockout**:
   - Triggered after 5 failed attempts (configurable)
   - Duration: 30 minutes (configurable)
   - Email notification sent on lockout
   - Automatic unlock after duration expires
   - Manual unlock available for administrators

4. **Failed Attempt Tracking**:
   - Counter increments on each failed login
   - Counter resets to 0 on successful login
   - Counter resets on password reset completion
   - Lockout end time cleared on successful login

5. **MFA Requirement**:
   - If `MfaEnabled = true`, second factor required
   - MFA method determined by `PreferredMfaMethod`
   - Initial login returns `requiresMfa: true`
   - Separate verification step required

6. **Session Creation**:
   - New session created on successful login
   - Session limit: 5 concurrent sessions (configurable)
   - Oldest session deleted when limit exceeded
   - Session includes device info and IP address

7. **Token Generation**:
   - Access token JWT with 15-minute expiry
   - Refresh token with 7-day expiry
   - Refresh token stored in User entity and UserSession
   - Tokens include claims: userId, email, role, tenantId

8. **Last Login Tracking**:
   - `LastLoginAt` updated on successful authentication
   - Used for analytics and security monitoring

---

### 4.3 User Logout (Sign Out)

**Endpoint**: `POST /api/session/revoke/{sessionId}` or `POST /api/session/revoke-all`

**Purpose**: Terminate user sessions and invalidate refresh tokens.

#### 4.3.1 Feature Overview

Logout functionality allows users to:
- Revoke a specific session (single device logout)
- Revoke all sessions (logout from all devices)
- Maintain security by invalidating refresh tokens

#### 4.3.2 Sequence Diagram - Logout Single Device

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SessionController
    participant SessionService
    participant SessionRepo
    participant UserRepo
    participant DynamoDB

    Note over Client: User clicks "Logout"

    Client->>API: POST /api/session/revoke/{sessionId}<br/>Authorization: Bearer {token}
    API->>SessionController: RevokeSessionCommand

    Note over SessionController: Validate JWT token
    SessionController->>SessionController: Extract userId from JWT

    SessionController->>SessionService: RevokeSessionAsync(userId, sessionId)

    Note over SessionService: Step 1: Find Session
    SessionService->>SessionRepo: GetByIdAsync(sessionId, userId)
    SessionRepo->>DynamoDB: GetItem<br/>PK: USER#{userId}<br/>SK: SESSION#{sessionId}

    alt Session Not Found
        DynamoDB-->>SessionRepo: Item not found
        SessionRepo-->>SessionService: null
        SessionService-->>SessionController: Session not found
        SessionController-->>Client: 404 Not Found<br/>{error: "Session not found"}
    end

    DynamoDB-->>SessionRepo: UserSession
    SessionRepo-->>SessionService: UserSession

    Note over SessionService: Step 2: Verify Ownership
    alt Session belongs to different user
        SessionService-->>SessionController: Unauthorized
        SessionController-->>Client: 403 Forbidden
    end

    Note over SessionService: Step 3: Mark Session Inactive
    SessionService->>SessionService: Set IsActive = false
    SessionService->>SessionService: Set LastAccessedAt = Now

    SessionService->>SessionRepo: UpdateAsync(session)
    SessionRepo->>DynamoDB: UpdateItem<br/>(Set IsActive=false)
    DynamoDB-->>SessionRepo: Updated

    Note over SessionService: Step 4: Invalidate Refresh Token in User
    SessionService->>UserRepo: GetByIdAsync(userId)
    UserRepo->>DynamoDB: GetItem
    DynamoDB-->>UserRepo: User

    SessionService->>SessionService: Check if session's refreshToken == user.RefreshToken

    alt This is the current refresh token
        SessionService->>SessionService: Clear user.RefreshToken
        SessionService->>SessionService: Clear user.RefreshTokenExpiry
        SessionService->>UserRepo: UpdateAsync(user)
        UserRepo->>DynamoDB: UpdateItem
    end

    SessionService-->>SessionController: Session revoked successfully

    SessionController-->>Client: 200 OK<br/>{success: true, message: "Session revoked"}

    Note over Client: Token is now invalid
    Note over Client: Subsequent API calls will fail
    Note over Client: User redirected to login page
```

#### 4.3.3 Sequence Diagram - Logout All Devices

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant SessionController
    participant SessionService
    participant SessionRepo
    participant UserRepo
    participant DynamoDB

    Note over Client: User clicks "Logout from all devices"

    Client->>API: POST /api/session/revoke-all<br/>Authorization: Bearer {token}
    API->>SessionController: RevokeAllSessionsCommand

    SessionController->>SessionController: Extract userId from JWT

    SessionController->>SessionService: RevokeAllSessionsAsync(userId, exceptCurrentSession)

    Note over SessionService: Step 1: Get All Active Sessions
    SessionService->>SessionRepo: GetActiveSessionsAsync(userId)
    SessionRepo->>DynamoDB: Query<br/>PK: USER#{userId}<br/>Filter: IsActive=true
    DynamoDB-->>SessionRepo: List<UserSession>
    SessionRepo-->>SessionService: Active sessions (e.g., 5 sessions)

    Note over SessionService: Step 2: Revoke Each Session
    loop For each session
        alt Is current session (optional keep-alive)
            SessionService->>SessionService: Skip current session
        else Other session
            SessionService->>SessionService: Set IsActive = false
            SessionService->>SessionRepo: UpdateAsync(session)
            SessionRepo->>DynamoDB: UpdateItem
        end
    end

    Note over SessionService: Step 3: Clear User Refresh Token
    SessionService->>UserRepo: GetByIdAsync(userId)
    UserRepo->>DynamoDB: GetItem
    DynamoDB-->>UserRepo: User

    SessionService->>SessionService: Clear user.RefreshToken
    SessionService->>SessionService: Clear user.RefreshTokenExpiry
    SessionService->>SessionService: Set ActiveSessionCount = 0 (or 1 if kept current)

    SessionService->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem

    SessionService-->>SessionController: All sessions revoked

    SessionController-->>Client: 200 OK<br/>{success: true, message: "Logged out from all devices", count: 5}

    Note over Client: All devices now logged out
    Note over Client: User redirected to login page
```

#### 4.3.4 Request/Response

**Request - Logout Single Device**:
```json
POST /api/session/revoke/sess-456
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "message": "Session revoked successfully"
}
```

**Request - Logout All Devices**:
```json
POST /api/session/revoke-all
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
X-Tenant-Id: tenant-001
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "message": "All sessions revoked successfully",
  "sessionsRevoked": 5
}
```

**Error Response - Session Not Found (404)**:
```json
{
  "success": false,
  "message": "Session not found"
}
```

#### 4.3.5 Business Rules

1. **Session Revocation**:
   - Session marked as `IsActive = false`
   - Refresh token cannot be used after revocation
   - Access tokens remain valid until natural expiry (15 minutes)
   - Session record remains in database for audit purposes

2. **Ownership Validation**:
   - Users can only revoke their own sessions
   - Session must belong to the authenticated user
   - Returns 403 Forbidden if attempting to revoke another user's session

3. **Refresh Token Invalidation**:
   - If revoking current session, user's refresh token is cleared
   - Prevents token refresh after logout
   - Forces re-authentication for new access tokens

4. **Logout All Devices**:
   - Revokes all active sessions for the user
   - Optional: Can keep current session active
   - Clears user's refresh token
   - Resets `ActiveSessionCount` to 0

5. **Security Considerations**:
   - Logout does not immediately invalidate access tokens (JWT stateless nature)
   - Access tokens remain valid for their remaining TTL
   - For immediate invalidation, implement token blacklist (future enhancement)
   - Sensitive operations should require re-authentication

---

### 4.4 Password Reset (Forgot Password)

**Endpoints**:
- `POST /api/password/forgot` - Request reset
- `POST /api/password/reset` - Complete reset

**Purpose**: Allow users to securely reset their password when forgotten.

#### 4.4.1 Feature Overview

The password reset feature provides a secure mechanism for users to recover access to their accounts:
- Request password reset via email
- Receive time-limited reset token
- Set new password with policy validation
- Invalidate all existing sessions
- Email notifications for security

#### 4.4.2 Sequence Diagram - Request Password Reset

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant ForgotPwdHandler
    participant UserRepo
    participant DynamoDB
    participant EmailService
    participant EmailTemplate
    participant SES

    Note over Client: User clicks "Forgot Password"

    Client->>API: POST /api/password/forgot<br/>{email: "user@example.com"}
    API->>ForgotPwdHandler: ForgotPasswordCommand

    Note over ForgotPwdHandler: Step 1: Find User
    ForgotPwdHandler->>UserRepo: GetByEmailAsync(email, tenantId)
    UserRepo->>DynamoDB: Query GSI1 (Email index)

    alt User Not Found
        DynamoDB-->>UserRepo: Not found
        UserRepo-->>ForgotPwdHandler: null

        Note over ForgotPwdHandler: Security: Don't reveal user existence
        ForgotPwdHandler-->>Client: 200 OK<br/>{message: "If account exists, email sent"}
        Note over Client: Generic success message
    end

    DynamoDB-->>UserRepo: User found
    UserRepo-->>ForgotPwdHandler: User

    Note over ForgotPwdHandler: Step 2: Generate Reset Token
    ForgotPwdHandler->>ForgotPwdHandler: GenerateSecureToken()
    ForgotPwdHandler->>ForgotPwdHandler: Create 32-byte random token
    ForgotPwdHandler->>ForgotPwdHandler: Base64 URL-safe encode

    Note over ForgotPwdHandler: Step 3: Set Token Expiry
    ForgotPwdHandler->>ForgotPwdHandler: Get TokenExpiryHours from config (1 hour)
    ForgotPwdHandler->>ForgotPwdHandler: Set PasswordResetTokenExpiry = Now + 1 hour

    Note over ForgotPwdHandler: Step 4: Save Token to User
    ForgotPwdHandler->>ForgotPwdHandler: Set user.PasswordResetToken = token
    ForgotPwdHandler->>ForgotPwdHandler: Set user.PasswordResetTokenExpiry = expiry

    ForgotPwdHandler->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem
    DynamoDB-->>UserRepo: Updated

    Note over ForgotPwdHandler: Step 5: Build Reset Link
    ForgotPwdHandler->>ForgotPwdHandler: Get WebAppUrl from config
    ForgotPwdHandler->>ForgotPwdHandler: resetLink = "{WebAppUrl}/reset-password?token={token}&email={email}"

    Note over ForgotPwdHandler: Step 6: Send Email
    ForgotPwdHandler->>EmailTemplate: RenderTemplateAsync("PasswordResetRequest", data)
    EmailTemplate->>EmailTemplate: Load PasswordResetRequest.html
    EmailTemplate->>EmailTemplate: Replace {{FirstName}} = "John"
    EmailTemplate->>EmailTemplate: Replace {{ResetLink}} = resetLink
    EmailTemplate->>EmailTemplate: Replace {{ExpiryHours}} = "1"
    EmailTemplate-->>ForgotPwdHandler: Rendered HTML

    ForgotPwdHandler->>EmailService: SendEmailAsync(user.Email, subject, html)
    EmailService->>SES: SendEmailAsync
    SES->>SES: Queue email for delivery
    SES-->>EmailService: MessageId: "01000192..."
    EmailService-->>ForgotPwdHandler: Email sent

    ForgotPwdHandler-->>Client: 200 OK<br/>{success: true, message: "If account exists, email sent"}

    Note over Client: User receives email
    Note over Client: User clicks reset link in email
```

#### 4.4.3 Sequence Diagram - Complete Password Reset

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant ResetPwdHandler
    participant UserRepo
    participant PasswordPolicy
    participant AccountLockout
    participant SessionRepo
    participant EmailService
    participant DynamoDB

    Note over Client: User clicks link from email
    Note over Client: User enters new password

    Client->>API: POST /api/password/reset<br/>{email, token, newPassword}
    API->>ResetPwdHandler: ResetPasswordCommand

    Note over ResetPwdHandler: Step 1: Find User
    ResetPwdHandler->>UserRepo: GetByEmailAsync(email, tenantId)
    UserRepo->>DynamoDB: Query GSI1

    alt User Not Found
        DynamoDB-->>UserRepo: Not found
        UserRepo-->>ResetPwdHandler: null
        ResetPwdHandler-->>Client: 400 Bad Request<br/>{error: "Invalid reset request"}
    end

    DynamoDB-->>UserRepo: User
    UserRepo-->>ResetPwdHandler: User

    Note over ResetPwdHandler: Step 2: Validate Token
    ResetPwdHandler->>ResetPwdHandler: Check user.PasswordResetToken == provided token

    alt Token Mismatch
        ResetPwdHandler-->>Client: 400 Bad Request<br/>{error: "Invalid reset token"}
    end

    ResetPwdHandler->>ResetPwdHandler: Check user.PasswordResetTokenExpiry > Now

    alt Token Expired
        ResetPwdHandler-->>Client: 400 Bad Request<br/>{error: "Reset token expired. Request new one"}
    end

    Note over ResetPwdHandler: Step 3: Validate New Password
    ResetPwdHandler->>PasswordPolicy: ValidatePassword(newPassword)
    PasswordPolicy->>PasswordPolicy: Check length >= 8
    PasswordPolicy->>PasswordPolicy: Check uppercase/lowercase/digit/special

    alt Password Invalid
        PasswordPolicy-->>ResetPwdHandler: ValidationResult(false, errors)
        ResetPwdHandler-->>Client: 400 Bad Request<br/>{errors: [...]}
    end

    PasswordPolicy-->>ResetPwdHandler: ValidationResult(true)

    Note over ResetPwdHandler: Step 4: Check Password History
    ResetPwdHandler->>PasswordPolicy: IsPasswordInHistory(newPassword, user)
    PasswordPolicy->>PasswordPolicy: Parse PasswordHistory (5 hashes)
    PasswordPolicy->>PasswordPolicy: BCrypt.Verify against each hash

    alt Password Recently Used
        PasswordPolicy-->>ResetPwdHandler: true
        ResetPwdHandler-->>Client: 400 Bad Request<br/>{error: "Cannot reuse recent password"}
    end

    PasswordPolicy-->>ResetPwdHandler: false (not in history)

    Note over ResetPwdHandler: Step 5: Hash New Password
    ResetPwdHandler->>PasswordPolicy: HashPassword(newPassword)
    PasswordPolicy->>PasswordPolicy: Generate salt
    PasswordPolicy->>PasswordPolicy: BCrypt.Hash(newPassword, salt)
    PasswordPolicy-->>ResetPwdHandler: newPasswordHash

    Note over ResetPwdHandler: Step 6: Update Password History
    ResetPwdHandler->>PasswordPolicy: AddToPasswordHistory(oldHash, user)
    PasswordPolicy->>PasswordPolicy: Add current hash to history
    PasswordPolicy->>PasswordPolicy: Keep last 5 hashes
    PasswordPolicy->>PasswordPolicy: Remove oldest if > 5
    PasswordPolicy-->>ResetPwdHandler: Updated history

    Note over ResetPwdHandler: Step 7: Update User
    ResetPwdHandler->>ResetPwdHandler: Set user.PasswordHash = newPasswordHash
    ResetPwdHandler->>ResetPwdHandler: Clear user.PasswordResetToken
    ResetPwdHandler->>ResetPwdHandler: Clear user.PasswordResetTokenExpiry
    ResetPwdHandler->>ResetPwdHandler: Set user.LastPasswordChangeAt = Now

    Note over ResetPwdHandler: Step 8: Reset Account Lockout
    ResetPwdHandler->>AccountLockout: ResetFailedLoginAttempts(user)
    AccountLockout->>AccountLockout: Set FailedLoginAttempts = 0
    AccountLockout->>AccountLockout: Clear LockoutEnd
    AccountLockout-->>ResetPwdHandler: Lockout cleared

    Note over ResetPwdHandler: Step 9: Save User
    ResetPwdHandler->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem
    DynamoDB-->>UserRepo: Updated

    Note over ResetPwdHandler: Step 10: Invalidate All Sessions
    ResetPwdHandler->>SessionRepo: RevokeAllSessionsAsync(userId)
    SessionRepo->>DynamoDB: Update all sessions (IsActive=false)
    DynamoDB-->>SessionRepo: Updated

    Note over ResetPwdHandler: Step 11: Send Success Email
    ResetPwdHandler->>EmailService: SendPasswordResetSuccessEmail(user)
    EmailService->>EmailService: Render template
    EmailService->>EmailService: Send via SES

    ResetPwdHandler-->>Client: 200 OK<br/>{success: true, message: "Password reset successfully"}

    Note over Client: User redirected to login page
    Note over Client: User must login with new password
```

#### 4.4.4 Request/Response

**Request - Forgot Password**:
```json
POST /api/password/forgot
Content-Type: application/json
X-Tenant-Id: tenant-001

{
  "email": "john.doe@example.com"
}
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "message": "If an account exists with this email, you will receive a password reset link."
}
```

**Request - Reset Password**:
```json
POST /api/password/reset
Content-Type: application/json
X-Tenant-Id: tenant-001

{
  "email": "john.doe@example.com",
  "resetToken": "Xk7mP2vQ9rN5wL8tF3hY6jB4nC1dS0aZ",
  "newPassword": "NewSecure@Pass456"
}
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "message": "Your password has been reset successfully. Please login with your new password."
}
```

**Error Responses**:

*Weak Password (400)*:
```json
{
  "success": false,
  "message": "Password must be at least 8 characters long. Password must contain at least one special character."
}
```

*Password in History (400)*:
```json
{
  "success": false,
  "message": "You cannot reuse a recent password. Please choose a different password."
}
```

*Invalid/Expired Token (400)*:
```json
{
  "success": false,
  "message": "Your password reset link has expired. Please request a new one."
}
```

#### 4.4.5 Business Rules

1. **Request Security**:
   - Always return success message (prevent user enumeration)
   - Don't reveal if email exists in system
   - Rate limit: Max 3 requests per hour per email (recommended)

2. **Reset Token**:
   - 32 bytes cryptographically secure random data
   - Base64 URL-safe encoding
   - Expiry: 1 hour (configurable)
   - Single-use: Invalidated after successful reset
   - Invalidated on new request (old tokens void)

3. **Password Validation**:
   - Must meet all password policy requirements
   - Cannot match current password hash
   - Cannot match any of last 5 passwords
   - Must be different from email address

4. **Security Actions**:
   - All active sessions invalidated on reset
   - Account lockout cleared (fresh start)
   - Failed login attempts reset to 0
   - Email notification sent to user

5. **Email Contents**:
   - **Request Email**: Reset link with token, expiry warning
   - **Success Email**: Confirmation of password change, security advice

6. **Timing Considerations**:
   - Email delivery: Async (don't wait for SES)
   - Token generation: Cryptographically secure
   - Response time: Consistent (prevent timing attacks)

---

### 4.5 Password Change (Authenticated User)

**Endpoint**: `POST /api/password/change`

**Purpose**: Allow authenticated users to change their password proactively.

#### 4.5.1 Feature Overview

Password change allows users to update their password while logged in:
- Requires current password verification
- Validates new password against policy
- Checks password history
- Invalidates all other sessions
- Sends confirmation email

#### 4.5.2 Sequence Diagram

```mermaid
sequenceDiagram
    participant Client
    participant API
    participant ChangePwdHandler
    participant UserRepo
    participant PasswordPolicy
    participant SessionService
    participant EmailService
    participant DynamoDB

    Note over Client: User in settings page
    Note over Client: User enters current and new password

    Client->>API: POST /api/password/change<br/>Authorization: Bearer {token}<br/>{currentPassword, newPassword}
    API->>ChangePwdHandler: ChangePasswordCommand

    Note over ChangePwdHandler: Extract userId from JWT
    ChangePwdHandler->>ChangePwdHandler: Get userId from JWT claims

    Note over ChangePwdHandler: Step 1: Load User
    ChangePwdHandler->>UserRepo: GetByIdAsync(userId, tenantId)
    UserRepo->>DynamoDB: GetItem
    DynamoDB-->>UserRepo: User
    UserRepo-->>ChangePwdHandler: User

    Note over ChangePwdHandler: Step 2: Verify Current Password
    ChangePwdHandler->>PasswordPolicy: VerifyPassword(currentPassword, user.PasswordHash)
    PasswordPolicy->>PasswordPolicy: BCrypt.Verify(currentPassword, hash)

    alt Current Password Invalid
        PasswordPolicy-->>ChangePwdHandler: false
        ChangePwdHandler-->>Client: 400 Bad Request<br/>{error: "Current password is incorrect"}
    end

    PasswordPolicy-->>ChangePwdHandler: true (current password correct)

    Note over ChangePwdHandler: Step 3: Validate New Password
    ChangePwdHandler->>PasswordPolicy: ValidatePassword(newPassword)
    PasswordPolicy->>PasswordPolicy: Check policy requirements

    alt New Password Invalid
        PasswordPolicy-->>ChangePwdHandler: ValidationResult(false, errors)
        ChangePwdHandler-->>Client: 400 Bad Request<br/>{errors: [...]}
    end

    PasswordPolicy-->>ChangePwdHandler: ValidationResult(true)

    Note over ChangePwdHandler: Step 4: Check Same as Current
    ChangePwdHandler->>PasswordPolicy: VerifyPassword(newPassword, user.PasswordHash)

    alt New Password Same as Current
        PasswordPolicy-->>ChangePwdHandler: true
        ChangePwdHandler-->>Client: 400 Bad Request<br/>{error: "New password must be different"}
    end

    PasswordPolicy-->>ChangePwdHandler: false (different password)

    Note over ChangePwdHandler: Step 5: Check Password History
    ChangePwdHandler->>PasswordPolicy: IsPasswordInHistory(newPassword, user)

    alt Password in History
        PasswordPolicy-->>ChangePwdHandler: true
        ChangePwdHandler-->>Client: 400 Bad Request<br/>{error: "Cannot reuse recent password"}
    end

    PasswordPolicy-->>ChangePwdHandler: false

    Note over ChangePwdHandler: Step 6: Hash New Password
    ChangePwdHandler->>PasswordPolicy: HashPassword(newPassword)
    PasswordPolicy-->>ChangePwdHandler: newPasswordHash

    Note over ChangePwdHandler: Step 7: Update Password History
    ChangePwdHandler->>PasswordPolicy: AddToPasswordHistory(currentHash, user)
    PasswordPolicy->>PasswordPolicy: Add current hash to history
    PasswordPolicy->>PasswordPolicy: Keep last 5
    PasswordPolicy-->>ChangePwdHandler: Updated history

    Note over ChangePwdHandler: Step 8: Update User
    ChangePwdHandler->>ChangePwdHandler: Set user.PasswordHash = newPasswordHash
    ChangePwdHandler->>ChangePwdHandler: Set user.LastPasswordChangeAt = Now
    ChangePwdHandler->>ChangePwdHandler: Clear user.RefreshToken (force re-login)
    ChangePwdHandler->>ChangePwdHandler: Clear user.RefreshTokenExpiry

    ChangePwdHandler->>UserRepo: UpdateAsync(user)
    UserRepo->>DynamoDB: UpdateItem
    DynamoDB-->>UserRepo: Updated

    Note over ChangePwdHandler: Step 9: Invalidate Other Sessions
    ChangePwdHandler->>SessionService: RevokeAllSessionsExceptCurrent(userId, currentSessionId)
    SessionService->>SessionService: Mark all other sessions inactive
    SessionService-->>ChangePwdHandler: Sessions revoked

    Note over ChangePwdHandler: Step 10: Send Email Notification
    ChangePwdHandler->>EmailService: SendPasswordChangedEmail(user)
    EmailService->>EmailService: Render template
    EmailService->>EmailService: Send via SES
    EmailService-->>ChangePwdHandler: Email sent

    ChangePwdHandler-->>Client: 200 OK<br/>{success: true, message: "Password changed successfully"}

    Note over Client: User remains logged in on current device
    Note over Client: All other devices logged out
```

#### 4.5.3 Request/Response

**Request**:
```json
POST /api/password/change
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json
X-Tenant-Id: tenant-001

{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecure@Pass456"
}
```

**Success Response (200 OK)**:
```json
{
  "success": true,
  "message": "Your password has been changed successfully. You have been logged out from all other devices."
}
```

**Error Responses**:

*Incorrect Current Password (400)*:
```json
{
  "success": false,
  "message": "Your current password is incorrect."
}
```

*Same Password (400)*:
```json
{
  "success": false,
  "message": "Your new password must be different from your current password."
}
```

*Password in History (400)*:
```json
{
  "success": false,
  "message": "You cannot reuse a recent password. Please choose a different password."
}
```

*Weak Password (400)*:
```json
{
  "success": false,
  "message": "Password must contain at least one uppercase letter. Password must contain at least one special character."
}
```

#### 4.5.4 Business Rules

1. **Authentication Required**:
   - Must have valid JWT token
   - User must be authenticated and active
   - Cannot change password for another user

2. **Current Password Verification**:
   - Must provide correct current password
   - Prevents unauthorized password changes if session hijacked
   - Adds extra security layer

3. **New Password Requirements**:
   - Must meet all password policy requirements
   - Must be different from current password
   - Cannot match any of last 5 passwords
   - Validated before database update

4. **Session Management**:
   - Current session remains active
   - All other sessions invalidated
   - Forces re-login on other devices
   - Refresh token cleared (except current)

5. **Notifications**:
   - Email sent to user's registered email
   - Includes timestamp of change
   - Security advice (not you? contact support)
   - Helps detect unauthorized changes

6. **Audit Trail**:
   - `LastPasswordChangeAt` timestamp updated
   - Password history maintained
   - Security monitoring can track frequency

---

_[Continued in next part due to length...]_

