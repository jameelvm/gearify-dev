using System;
using System.Collections.Generic;
using System.Linq;
using Gearify.CartService.API.Models;
using Gearify.CartService.Domain.Entities;

namespace Gearify.CartService.Application.Mappers;

public static class CartMapper
{
    public static CartResponse ToResponse(Cart cart)
    {
        var items = cart.Items.Select(ToItemResponse).ToList();

        return new CartResponse(
            Id: cart.Id,
            UserId: cart.UserId,
            Items: items,
            Subtotal: cart.TotalAmount,
            Total: cart.TotalAmount,
            Currency: cart.Currency,
            ItemCount: cart.Items.Count,
            CreatedAt: cart.CreatedAt,
            UpdatedAt: cart.UpdatedAt
        );
    }

    public static CartResponse EmptyCart(string userId)
    {
        return new CartResponse(
            Id: string.Empty,
            UserId: userId,
            Items: new List<CartItemResponse>(),
            Subtotal: 0m,
            Total: 0m,
            Currency: "USD",
            ItemCount: 0,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow
        );
    }

    private static CartItemResponse ToItemResponse(CartItem item)
    {
        return new CartItemResponse(
            ProductId: item.ProductId,
            ProductName: item.ProductName,
            Sku: item.Sku,
            ImageUrl: item.ImageUrl,
            Quantity: item.Quantity,
            UnitPrice: item.Price,
            LineTotal: item.Price * item.Quantity
        );
    }
}
