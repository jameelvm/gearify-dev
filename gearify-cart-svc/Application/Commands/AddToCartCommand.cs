using Gearify.CartService.Domain.Entities;
using MediatR;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(
    string UserId,
    string TenantId,
    string ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal Price,
    string? ImageUrl = null
) : IRequest<AddToCartResult>;

public record AddToCartResult(bool Success, Cart? Cart = null, string? ErrorMessage = null);
