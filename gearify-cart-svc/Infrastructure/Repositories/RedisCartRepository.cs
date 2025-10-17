using Gearify.CartService.Domain.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace Gearify.CartService.Infrastructure.Repositories;

public class RedisCartRepository : ICartRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _cartExpiration = TimeSpan.FromDays(7);

    public RedisCartRepository(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<Cart?> GetCartAsync(string userId, string tenantId)
    {
        var db = _redis.GetDatabase();
        var key = GetCartKey(userId, tenantId);
        var value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<Cart>(value!);
    }

    public async Task SaveCartAsync(Cart cart)
    {
        var db = _redis.GetDatabase();
        var key = GetCartKey(cart.UserId, cart.TenantId);
        var value = JsonSerializer.Serialize(cart);

        await db.StringSetAsync(key, value, _cartExpiration);
    }

    public async Task DeleteCartAsync(string userId, string tenantId)
    {
        var db = _redis.GetDatabase();
        var key = GetCartKey(userId, tenantId);
        await db.KeyDeleteAsync(key);
    }

    private string GetCartKey(string userId, string tenantId) => $"cart:{tenantId}:{userId}";
}
