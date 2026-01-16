using Gearify.CartService.Domain.Entities;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record UpdateCartItemCommand(
    string UserId,
    string ProductId,
    int Quantity
) : IRequest<UpdateCartItemResult>;

public record UpdateCartItemResult(bool Success, Cart? Cart = null, string? ErrorMessage = null);
