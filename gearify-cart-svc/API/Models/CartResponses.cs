using System;
using System.Collections.Generic;

namespace Gearify.CartService.API.Models;

public record CartResponse(
    string Id,
    string UserId,
    List<CartItemResponse> Items,
    decimal Subtotal,
    decimal Total,
    string Currency,
    int ItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CartItemResponse(
    string ProductId,
    string ProductName,
    string Sku,
    string ImageUrl,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);
