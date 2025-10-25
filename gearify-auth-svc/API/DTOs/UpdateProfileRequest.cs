namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for updating user profile
/// </summary>
public record UpdateProfileRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null
);
