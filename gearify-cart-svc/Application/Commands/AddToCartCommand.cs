using Amazon.DynamoDBv2.DataModel;
using Gearify.CartService.Domain;
using MediatR;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(string UserId, string ProductId, int Quantity, decimal Price) : IRequest<Cart>;

public class AddToCartHandler : IRequestHandler<AddToCartCommand, Cart>
{
    private readonly IDynamoDBContext _dynamoContext;
    private readonly IConnectionMultiplexer _redis;

    public AddToCartHandler(IDynamoDBContext dynamoContext, IConnectionMultiplexer redis)
    {
        _dynamoContext = dynamoContext;
        _redis = redis;
    }

    public async Task<Cart> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var cacheKey = "cart:" + cmd.UserId;

        var cart = await _dynamoContext.LoadAsync<Cart>(cmd.UserId, ct) ?? new Cart { UserId = cmd.UserId };

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == cmd.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += cmd.Quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = cmd.ProductId,
                ProductName = "Product " + cmd.ProductId,
                Quantity = cmd.Quantity,
                Price = cmd.Price
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _dynamoContext.SaveAsync(cart, ct);
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(cart), TimeSpan.FromMinutes(15));

        return cart;
    }
}
