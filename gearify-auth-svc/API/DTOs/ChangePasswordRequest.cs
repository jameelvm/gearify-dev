namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for changing user password
/// </summary>
public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);
