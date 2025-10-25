namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// User data transfer object
/// </summary>
public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string Phone,
    string Role,
    bool IsActive,
    bool EmailVerified,
    DateTime? LastLoginAt
);
