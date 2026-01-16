using Gearify.CartService.Domain.Entities;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(
    string UserId,
    string ProductId,
    int Quantity = 1
) : IRequest<AddToCartResult>;

public record AddToCartResult(bool Success, Cart? Cart = null, string? ErrorMessage = null);
