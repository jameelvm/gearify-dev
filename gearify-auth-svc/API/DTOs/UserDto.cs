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
    DateTime? LastLoginAt,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? Country = null
);
