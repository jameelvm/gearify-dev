namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for user login
/// </summary>
public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);
