namespace Gearify.AuthService.Application.Models;

/// <summary>
/// Result of password validation against policy
/// </summary>
public record PasswordValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();

    public static PasswordValidationResult Success() => new() { IsValid = true };

    public static PasswordValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
