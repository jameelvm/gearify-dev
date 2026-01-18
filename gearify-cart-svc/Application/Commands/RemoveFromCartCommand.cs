using Gearify.CartService.API.Models;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record RemoveFromCartCommand(
    string UserId,
    string ProductId
) : IRequest<RemoveFromCartResult>;

public record RemoveFromCartResult(bool Success, CartResponse? Cart = null, string? ErrorMessage = null);
