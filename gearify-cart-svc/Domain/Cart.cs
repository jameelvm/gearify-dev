using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace Gearify.CartService.Domain;

[DynamoDBTable("gearify-carts")]
public class Cart
{
    [DynamoDBHashKey]
    public string UserId { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CartItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
