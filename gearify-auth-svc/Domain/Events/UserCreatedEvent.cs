using MediatR;

namespace Gearify.AuthService.Domain.Events;

/// <summary>
/// Event raised when a new user is created in the system
/// </summary>
public record UserCreatedEvent(
    string UserId,
    string TenantId,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    DateTime CreatedAt
) : INotification;
