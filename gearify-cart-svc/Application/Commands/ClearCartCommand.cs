using MediatR;

namespace Gearify.CartService.Application.Commands;

public record ClearCartCommand(string UserId) : IRequest<ClearCartResult>;

public record ClearCartResult(bool Success, string? ErrorMessage = null);
