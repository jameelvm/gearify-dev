namespace Gearify.AuthService.Application.Models;

/// <summary>
/// Result of MFA setup operation
/// </summary>
public record MfaSetupResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? QrCodeBase64 { get; init; }
    public string? ManualEntryKey { get; init; }
    public string[]? BackupCodes { get; init; }
}

/// <summary>
/// Result of MFA verification
/// </summary>
public record MfaVerificationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Token { get; init; }
    public string? RefreshToken { get; init; }
}
