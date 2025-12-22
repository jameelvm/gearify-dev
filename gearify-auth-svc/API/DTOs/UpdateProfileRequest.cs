namespace Gearify.AuthService.API.DTOs;

/// <summary>
/// Request DTO for updating user profile
/// </summary>
public record UpdateProfileRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? Country = null
);
