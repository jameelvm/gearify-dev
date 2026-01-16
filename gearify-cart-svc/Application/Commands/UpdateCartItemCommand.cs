using Gearify.CartService.API.Models;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record UpdateCartItemCommand(
    string UserId,
    string ProductId,
    int Quantity
) : IRequest<UpdateCartItemResult>;

public record UpdateCartItemResult(bool Success, CartResponse? Cart = null, string? ErrorMessage = null);
