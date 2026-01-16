using Gearify.CartService.API.Models;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(
    string UserId,
    string ProductId,
    int Quantity = 1
) : IRequest<AddToCartResult>;

public record AddToCartResult(bool Success, CartResponse? Cart = null, string? ErrorMessage = null);
