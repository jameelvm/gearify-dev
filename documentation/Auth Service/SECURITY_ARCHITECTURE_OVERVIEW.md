# Security Features - Architecture Overview

## System Architecture Diagram

```mermaid
graph TB
    subgraph "Client Layer"
        WebApp[Web Application]
        MobileApp[Mobile App]
        AuthApp[Authenticator App]
    end

    subgraph "API Gateway"
        Gateway[API Gateway]
        TenantMiddleware[Tenant Resolution]
        AuthMiddleware[JWT Auth]
    end

    subgraph "Auth Service Controllers"
        AuthController[AuthController]
        PasswordController[PasswordController]
        MfaController[MfaController]
        SessionController[SessionController]
    end

    subgraph "Application Layer - Commands & Queries"
        LoginCmd[LoginCommand]
        RegisterCmd[RegisterCommand]
        ForgotPwdCmd[ForgotPasswordCommand]
        ResetPwdCmd[ResetPasswordCommand]
        ChangePwdCmd[ChangePasswordCommand]
        SetupMfaCmd[SetupTotpMfaCommand]
        VerifyMfaCmd[VerifyMfaSetupCommand]
        DisableMfaCmd[DisableMfaCommand]
        RevokeSessionCmd[RevokeSessionCommand]
        GetSessionsQuery[GetActiveSessionsQuery]
    end

    subgraph "Application Services"
        PasswordPolicy[PasswordPolicyService]
        AccountLockout[AccountLockoutService]
        TotpService[TotpService]
        OtpService[OtpService]
        SmsService[SmsService]
        SessionService[SessionService]
        EmailService[EmailService]
        EmailTemplate[EmailTemplateService]
        JwtService[JwtService]
    end

    subgraph "Infrastructure - Repositories"
        UserRepo[UserRepository]
        SessionRepo[UserSessionRepository]
        MfaCodeRepo[MfaCodeRepository]
    end

    subgraph "Data Layer"
        DynamoDB[(DynamoDB)]
        UsersTable[(Users Table)]
        SessionsTable[(UserSessions Table)]
        MfaCodesTable[(MfaCodes Table)]
    end

    subgraph "External Services"
        SES[AWS SES - Email]
        SNS[AWS SNS - SMS]
        LocalStack[LocalStack - Dev]
    end

    WebApp --> Gateway
    MobileApp --> Gateway
    AuthApp -.->|Scan QR Code| MobileApp

    Gateway --> TenantMiddleware
    TenantMiddleware --> AuthMiddleware
    AuthMiddleware --> AuthController
    AuthMiddleware --> PasswordController
    AuthMiddleware --> MfaController
    AuthMiddleware --> SessionController

    AuthController --> LoginCmd
    AuthController --> RegisterCmd
    PasswordController --> ForgotPwdCmd
    PasswordController --> ResetPwdCmd
    PasswordController --> ChangePwdCmd
    MfaController --> SetupMfaCmd
    MfaController --> VerifyMfaCmd
    MfaController --> DisableMfaCmd
    SessionController --> RevokeSessionCmd
    SessionController --> GetSessionsQuery

    LoginCmd --> AccountLockout
    LoginCmd --> PasswordPolicy
    LoginCmd --> JwtService
    LoginCmd --> SessionService
    RegisterCmd --> PasswordPolicy
    ForgotPwdCmd --> EmailService
    ResetPwdCmd --> PasswordPolicy
    ResetPwdCmd --> EmailService
    ChangePwdCmd --> PasswordPolicy
    ChangePwdCmd --> EmailService
    SetupMfaCmd --> TotpService
    SetupMfaCmd --> EmailService
    VerifyMfaCmd --> TotpService
    VerifyMfaCmd --> EmailService
    DisableMfaCmd --> EmailService
    LoginCmd --> OtpService
    OtpService --> SmsService
    RevokeSessionCmd --> SessionService
    GetSessionsQuery --> SessionService

    PasswordPolicy --> UserRepo
    AccountLockout --> UserRepo
    AccountLockout --> EmailService
    SessionService --> SessionRepo
    OtpService --> MfaCodeRepo
    EmailService --> EmailTemplate
    EmailService --> SES

    SmsService --> SNS

    UserRepo --> DynamoDB
    SessionRepo --> DynamoDB
    MfaCodeRepo --> DynamoDB

    DynamoDB --> UsersTable
    DynamoDB --> SessionsTable
    DynamoDB --> MfaCodesTable

    SES -.-> LocalStack
    SNS -.-> LocalStack

    style WebApp fill:#e1f5ff
    style MobileApp fill:#e1f5ff
    style AuthApp fill:#e1f5ff
    style Gateway fill:#fff4e6
    style TenantMiddleware fill:#fff4e6
    style AuthMiddleware fill:#fff4e6
    style PasswordPolicy fill:#f3e5f5
    style AccountLockout fill:#f3e5f5
    style TotpService fill:#f3e5f5
    style SessionService fill:#f3e5f5
    style DynamoDB fill:#e8f5e9
    style SES fill:#ffebee
    style SNS fill:#ffebee
```

---

## Component Responsibility Matrix

| Component | Responsibility | Key Methods |
|-----------|----------------|-------------|
| **PasswordPolicyService** | Password validation, hashing, history | ValidatePassword(), HashPassword(), IsPasswordInHistory() |
| **AccountLockoutService** | Lockout tracking and enforcement | IsLockedOut(), RecordFailedLoginAttempt(), ResetFailedLoginAttempts() |
| **TotpService** | TOTP generation and verification | GenerateSecret(), GenerateQrCode(), VerifyCode() |
| **OtpService** | Email/SMS OTP management | GenerateCode(), StoreCodeAsync(), VerifyCodeAsync() |
| **SmsService** | SMS delivery via AWS SNS | SendSmsAsync(), SendOtpAsync() |
| **SessionService** | Session lifecycle management | CreateSessionAsync(), RevokeSessionAsync(), GetActiveSessionsAsync() |
| **EmailService** | Email delivery via AWS SES | SendEmailAsync() |
| **EmailTemplateService** | Template rendering | RenderTemplateAsync() |
| **JwtService** | JWT token generation | GenerateAccessToken(), GenerateRefreshToken() |

---

## Data Flow Diagrams

### 1. Password Policy Enforcement Flow

```mermaid
flowchart LR
    Input[User Input: Password] --> Validate{Validate Policy}
    Validate -->|Invalid| Error[Return Errors]
    Validate -->|Valid| History{Check History}
    History -->|In History| Error2[Return Error: Recently Used]
    History -->|Not In History| Hash[Hash Password]
    Hash --> Store[Store Hash]
    Store --> AddHistory[Add to History]
    AddHistory --> Success[Success]

    style Input fill:#e3f2fd
    style Validate fill:#fff9c4
    style History fill:#fff9c4
    style Hash fill:#c8e6c9
    style Success fill:#c8e6c9
    style Error fill:#ffcdd2
    style Error2 fill:#ffcdd2
```

### 2. Account Lockout Flow

```mermaid
flowchart TB
    Login[Login Attempt] --> CheckLocked{Already Locked?}
    CheckLocked -->|Yes| ReturnError[Return: Account Locked]
    CheckLocked -->|No| VerifyPwd{Password Valid?}
    VerifyPwd -->|Yes| Reset[Reset Failed Attempts]
    Reset --> Success[Login Success]
    VerifyPwd -->|No| Increment[Increment Failed Attempts]
    Increment --> CheckCount{Count >= Max?}
    CheckCount -->|Yes| LockAccount[Set Lockout End Time]
    LockAccount --> SendEmail[Send Account Locked Email]
    SendEmail --> ReturnLocked[Return: Account Locked]
    CheckCount -->|No| ReturnFail[Return: Invalid Credentials]

    style Login fill:#e3f2fd
    style CheckLocked fill:#fff9c4
    style VerifyPwd fill:#fff9c4
    style CheckCount fill:#fff9c4
    style Success fill:#c8e6c9
    style LockAccount fill:#ffcdd2
    style ReturnError fill:#ffcdd2
    style ReturnLocked fill:#ffcdd2
    style ReturnFail fill:#ffcdd2
```

### 3. MFA Setup and Verification Flow

```mermaid
flowchart TB
    Setup[User Requests MFA Setup] --> Generate[Generate TOTP Secret]
    Generate --> QR[Generate QR Code]
    QR --> Backup[Generate Backup Codes]
    Backup --> StoreTemp[Store in DB - Not Active]
    StoreTemp --> Return[Return QR & Codes to User]
    Return --> UserScan[User Scans QR Code]
    UserScan --> EnterCode[User Enters Code from App]
    EnterCode --> Verify{Verify Code}
    Verify -->|Invalid| ReturnError[Return: Invalid Code]
    Verify -->|Valid| Enable[Set MfaEnabled=true]
    Enable --> SendEmail[Send MFA Enabled Email]
    SendEmail --> Success[Return: MFA Enabled]

    style Setup fill:#e3f2fd
    style Generate fill:#fff9c4
    style Verify fill:#fff9c4
    style Enable fill:#c8e6c9
    style Success fill:#c8e6c9
    style ReturnError fill:#ffcdd2
```

### 4. Session Management Flow

```mermaid
flowchart LR
    Login[User Login] --> CreateSession[Create Session Record]
    CreateSession --> CheckCount{Session Count > Max?}
    CheckCount -->|Yes| DeleteOldest[Delete Oldest Session]
    CheckCount -->|No| Store[Store Session]
    DeleteOldest --> Store
    Store --> Success[Return Token]

    ViewSessions[View Active Sessions] --> Query[Query User Sessions]
    Query --> Filter[Filter Active & Not Expired]
    Filter --> ReturnList[Return Session List]

    Revoke[Revoke Session] --> Update[Set IsActive=false]
    Update --> RevokeSuccess[Return Success]

    style Login fill:#e3f2fd
    style ViewSessions fill:#e3f2fd
    style Revoke fill:#e3f2fd
    style CheckCount fill:#fff9c4
    style Success fill:#c8e6c9
    style RevokeSuccess fill:#c8e6c9
```

---

## Security Layers

### Layer 1: Transport Security
- **HTTPS Only** in production
- **TLS 1.2+** encryption
- **CORS** configured for allowed origins

### Layer 2: Authentication
- **JWT Bearer Tokens** with expiry
- **Refresh Tokens** for session management
- **Multi-Factor Authentication** (TOTP/Email/SMS)

### Layer 3: Authorization
- **Role-Based Access Control** (RBAC)
- **Tenant Isolation** via middleware
- **Endpoint-level authorization** via [Authorize] attribute

### Layer 4: Password Security
- **BCrypt Hashing** (adaptive, salted)
- **Password Policy** enforcement
- **Password History** tracking (5 passwords)
- **Secure Token Generation** (cryptographic RNG)

### Layer 5: Account Protection
- **Account Lockout** after failed attempts
- **Progressive Lockout** (exponential backoff possible)
- **Email Notifications** for security events
- **Session Management** and revocation

### Layer 6: Audit & Monitoring
- **Failed Login Tracking**
- **Password Change Timestamps**
- **MFA Setup Tracking**
- **Session Activity Logging**

---

## Integration Points

### AWS Services

```mermaid
graph LR
    AuthService[Auth Service] --> DynamoDB[DynamoDB]
    AuthService --> SES[SES - Email]
    AuthService --> SNS[SNS - SMS]

    DynamoDB --> Users[(Users Table)]
    DynamoDB --> Sessions[(Sessions Table)]
    DynamoDB --> MfaCodes[(MFA Codes Table)]

    SES --> UserEmail[User Email]
    SNS --> UserPhone[User Phone]

    LocalStack[LocalStack] -.->|Development| DynamoDB
    LocalStack -.->|Development| SES
    LocalStack -.->|Development| SNS

    style AuthService fill:#e1f5ff
    style DynamoDB fill:#e8f5e9
    style SES fill:#ffebee
    style SNS fill:#ffebee
    style LocalStack fill:#fff9c4
```

### External Integrations

| Service | Purpose | Fallback |
|---------|---------|----------|
| **AWS SES** | Email delivery | SMTP server |
| **AWS SNS** | SMS delivery | Twilio, custom provider |
| **LocalStack** | Local AWS emulation | Direct AWS in production |
| **Authenticator Apps** | TOTP generation | Backup codes |

---

## Technology Stack

### Backend
- **.NET 8** - Runtime framework
- **ASP.NET Core** - Web framework
- **MediatR** - CQRS pattern
- **FluentValidation** - Input validation
- **Serilog** - Structured logging
- **OpenTelemetry** - Observability

### Security Libraries
- **BCrypt.Net** - Password hashing
- **OtpNet** (v1.9.2) - TOTP implementation
- **QRCoder** (v1.4.3) - QR code generation
- **System.Security.Cryptography** - Token generation

### AWS SDK
- **AWSSDK.DynamoDBv2** - DynamoDB client
- **AWSSDK.SimpleEmailService** - SES client
- **AWSSDK.SimpleNotificationService** - SNS client

### Development Tools
- **LocalStack** - Local AWS emulation
- **Docker** - Containerization
- **Swagger/OpenAPI** - API documentation

---

## Deployment Architecture

### Development Environment

```mermaid
graph TB
    Dev[Developer Machine] --> Docker[Docker Desktop]
    Docker --> LocalStack[LocalStack Container]
    Docker --> AuthService[Auth Service Container]
    Docker --> Redis[Redis Container]

    LocalStack --> DynamoDB[DynamoDB Local]
    LocalStack --> SES[SES Local]
    LocalStack --> SNS[SNS Local]

    AuthService --> LocalStack
    AuthService --> Redis

    style Dev fill:#e1f5ff
    style Docker fill:#fff4e6
    style LocalStack fill:#e8f5e9
    style AuthService fill:#f3e5f5
```

### Production Environment

```mermaid
graph TB
    Internet[Internet] --> LB[Load Balancer]
    LB --> ASG[Auto Scaling Group]

    ASG --> AuthService1[Auth Service Instance 1]
    ASG --> AuthService2[Auth Service Instance 2]
    ASG --> AuthService3[Auth Service Instance 3]

    AuthService1 --> DynamoDB[DynamoDB]
    AuthService2 --> DynamoDB
    AuthService3 --> DynamoDB

    AuthService1 --> SES[AWS SES]
    AuthService2 --> SES
    AuthService3 --> SES

    AuthService1 --> SNS[AWS SNS]
    AuthService2 --> SNS
    AuthService3 --> SNS

    DynamoDB --> Backup[DynamoDB Backups]

    CloudWatch[CloudWatch] --> AuthService1
    CloudWatch --> AuthService2
    CloudWatch --> AuthService3

    style Internet fill:#e1f5ff
    style LB fill:#fff4e6
    style ASG fill:#fff9c4
    style DynamoDB fill:#e8f5e9
    style SES fill:#ffebee
    style SNS fill:#ffebee
```

---

## Performance Considerations

### Database Optimization

| Table | Access Pattern | Index/Key | Notes |
|-------|----------------|-----------|-------|
| Users | GetByEmail | GSI1: Email | Most common lookup |
| Users | GetById | PK: USER#{id} | Primary access |
| UserSessions | GetByUser | PK: USER#{userId} | List user sessions |
| UserSessions | GetByToken | Linear scan | Could add GSI |
| MfaCodes | GetByUser | PK: USER#{userId} | Cleanup old codes |

### Caching Strategy

```mermaid
graph LR
    Request[API Request] --> Cache{In Cache?}
    Cache -->|Hit| Return[Return Cached]
    Cache -->|Miss| DB[Query DynamoDB]
    DB --> Store[Store in Cache]
    Store --> Return

    style Request fill:#e1f5ff
    style Cache fill:#fff9c4
    style DB fill:#e8f5e9
    style Return fill:#c8e6c9
```

**Cacheable Data**:
- ✅ User profile (after login)
- ✅ MFA settings
- ❌ Session tokens (security)
- ❌ OTP codes (security)

### Rate Limiting (Recommended)

| Endpoint | Rate Limit | Window | Notes |
|----------|-----------|--------|-------|
| /login | 5 attempts | 15 min | Per IP address |
| /password/forgot | 3 requests | 1 hour | Per email |
| /password/reset | 5 attempts | 1 hour | Per token |
| /mfa/verify | 3 attempts | 5 min | Per code |

---

## Monitoring & Alerts

### Key Metrics to Monitor

```mermaid
graph TB
    Metrics[Monitoring System] --> Auth[Authentication Metrics]
    Metrics --> Security[Security Metrics]
    Metrics --> Performance[Performance Metrics]

    Auth --> LoginRate[Login Success/Fail Rate]
    Auth --> MfaRate[MFA Adoption Rate]
    Auth --> TokenRefresh[Token Refresh Rate]

    Security --> Lockouts[Account Lockout Events]
    Security --> FailedLogins[Failed Login Attempts]
    Security --> PwdResets[Password Reset Requests]
    Security --> SessionRevokes[Session Revocations]

    Performance --> ResponseTime[API Response Time]
    Performance --> DBLatency[Database Latency]
    Performance --> EmailLatency[Email Delivery Time]

    style Metrics fill:#e1f5ff
    style Auth fill:#f3e5f5
    style Security fill:#ffebee
    style Performance fill:#e8f5e9
```

### Alert Thresholds

| Alert | Condition | Action |
|-------|-----------|--------|
| High Failed Logins | > 100 fails/min | Investigate potential attack |
| Mass Lockouts | > 50 lockouts/hour | Check for credential stuffing |
| High Password Resets | > 500 resets/hour | Possible phishing campaign |
| DB Throttling | > 10 throttles/min | Scale DynamoDB capacity |
| SES Bounce Rate | > 5% | Review email list |

---

## Scalability Considerations

### Horizontal Scaling
- **Stateless Services** - All state in DynamoDB
- **No In-Memory Sessions** - Database-backed sessions
- **Load Balancer Ready** - Round-robin distribution

### Database Scaling
- **DynamoDB Auto-Scaling** - On-demand or provisioned
- **Global Tables** - Multi-region replication (future)
- **Point-in-Time Recovery** - Continuous backups

### Service Limits

| AWS Service | Limit | Scalability |
|-------------|-------|-------------|
| SES Emails | 14 emails/sec (sandbox) | Request limit increase |
| SNS SMS | 20 TPS (default) | Request limit increase |
| DynamoDB | 40,000 RCU/WCU per table | On-demand auto-scales |
| Lambda (future) | 1000 concurrent | Request limit increase |

---

## Disaster Recovery

### Backup Strategy

```mermaid
graph LR
    DB[DynamoDB Tables] --> PITR[Point-in-Time Recovery]
    DB --> OnDemand[On-Demand Backups]
    DB --> Export[Data Export to S3]

    PITR --> Restore1[Restore to any point in last 35 days]
    OnDemand --> Restore2[Restore from specific backup]
    Export --> Analytics[Analytics & Compliance]

    style DB fill:#e8f5e9
    style PITR fill:#e1f5ff
    style OnDemand fill:#e1f5ff
    style Export fill:#fff9c4
```

### RTO/RPO Targets

| Scenario | RTO | RPO | Strategy |
|----------|-----|-----|----------|
| Service Failure | < 5 min | 0 | Auto-scaling, health checks |
| Database Failure | < 1 hour | < 5 min | DynamoDB auto-failover |
| Region Failure | < 4 hours | < 1 hour | Multi-region (future) |
| Data Corruption | < 24 hours | < 1 hour | Point-in-time recovery |

---

## Summary

This architecture provides:
- ✅ **Scalable** - Horizontal scaling with stateless services
- ✅ **Secure** - Multiple security layers (transport, auth, password, account)
- ✅ **Resilient** - Database backups, auto-scaling, monitoring
- ✅ **Maintainable** - Clean architecture, CQRS pattern, dependency injection
- ✅ **Observable** - Structured logging, metrics, distributed tracing
- ✅ **Testable** - Service interfaces, dependency injection, isolated components

**Architecture Pattern**: Clean Architecture + CQRS + Event-Driven
**Cloud Provider**: AWS (DynamoDB, SES, SNS)
**Development**: LocalStack for local AWS emulation
**Monitoring**: Serilog + OpenTelemetry + CloudWatch

---

**Document Version**: 1.0
**Last Updated**: October 26, 2025
**Architecture Owner**: Gearify Platform Team
