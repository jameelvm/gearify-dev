using MediatR;

namespace Gearify.AuthService.Application.Commands;

/// <summary>
/// Command to update user profile information
/// </summary>
public record UpdateProfileCommand(
    string UserId,
    string? FirstName,
    string? LastName,
    string? Phone
) : IRequest<UpdateProfileResult>;

/// <summary>
/// Result of profile update operation
/// </summary>
public record UpdateProfileResult(
    bool Success,
    string? ErrorMessage = null
);
