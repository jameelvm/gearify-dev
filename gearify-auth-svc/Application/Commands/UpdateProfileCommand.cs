using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to update user profile information
/// </summary>
public record UpdateProfileCommand(
    string UserId,
    string? FirstName,
    string? LastName,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? ZipCode,
    string? Country
) : IRequest<UpdateProfileResult>;

/// <summary>
/// Result of profile update operation
/// </summary>
public record UpdateProfileResult(
    bool Success,
    string? ErrorMessage = null
);
