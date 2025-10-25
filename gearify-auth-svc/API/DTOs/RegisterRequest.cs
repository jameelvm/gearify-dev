namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for user registration
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone = null,
    string? Role = null
);
