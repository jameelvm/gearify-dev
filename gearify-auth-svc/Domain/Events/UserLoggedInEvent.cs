using MediatR;

namespace Gearify.AuthService.Domain.Events;

/// <summary>
/// Event raised when a user successfully logs in
/// </summary>
public record UserLoggedInEvent(
    string UserId,
    string TenantId,
    string Email,
    DateTime LoginAt
) : INotification;
