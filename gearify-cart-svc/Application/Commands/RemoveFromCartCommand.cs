using MediatR;

namespace Gearify.CartService.Application.Commands;

public record RemoveFromCartCommand(
    string UserId,
    string TenantId,
    string ProductId
) : IRequest<RemoveFromCartResult>;

public record RemoveFromCartResult(bool Success, string? ErrorMessage = null);
