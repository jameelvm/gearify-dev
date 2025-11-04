namespace Gearify.AuthService.Application.Models;

/// <summary>
/// Security configuration settings
/// </summary>
public class SecurityConfiguration
{
    public PasswordPolicySettings PasswordPolicy { get; set; } = new();
    public AccountLockoutSettings AccountLockout { get; set; } = new();
    public MfaSettings Mfa { get; set; } = new();
    public PasswordResetSettings PasswordReset { get; set; } = new();
    public SessionSettings Session { get; set; } = new();
}

/// <summary>
/// Password policy settings
/// </summary>
public class PasswordPolicySettings
{
    public int MinimumLength { get; set; } = 8;
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigit { get; set; } = true;
    public bool RequireSpecialChar { get; set; } = true;
    public int PasswordHistoryCount { get; set; } = 5;
}

/// <summary>
/// Account lockout settings
/// </summary>
public class AccountLockoutSettings
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 30;
    public bool EnableLockout { get; set; } = true;
}

/// <summary>
/// MFA settings
/// </summary>
public class MfaSettings
{
    public int CodeExpiryMinutes { get; set; } = 5;
    public int MaxVerificationAttempts { get; set; } = 3;
    public int BackupCodesCount { get; set; } = 10;
    public string TotpIssuer { get; set; } = "Gearify";
    public int TotpDigits { get; set; } = 6;
    public int TotpPeriod { get; set; } = 30;
}

/// <summary>
/// Password reset settings
/// </summary>
public class PasswordResetSettings
{
    public int TokenExpiryHours { get; set; } = 1;
    public int MaxResetAttemptsPerDay { get; set; } = 3;
}

/// <summary>
/// Session settings
/// </summary>
public class SessionSettings
{
    public int MaxConcurrentSessions { get; set; } = 5;
    public int SessionTimeoutMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}

/// <summary>
/// SMS configuration
/// </summary>
public class SmsConfiguration
{
    public string Provider { get; set; } = "AWS_SNS";
    public string FromNumber { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = "us-east-1";
}
