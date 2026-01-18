using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Infrastructure.Caching;

/// <summary>
/// Cart-specific caching implementation
/// </summary>
public class CartCacheService : ICartCacheService
{
    private readonly ICacheService _cache;
    private readonly ILogger<CartCacheService> _logger;

    public CartCacheService(ICacheService cache, ILogger<CartCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Cart?> GetAsync(string userId, string tenantId)
    {
        var key = GetCacheKey(userId, tenantId);
        var cart = await _cache.GetAsync<Cart>(key);

        if (cart != null)
        {
            _logger.LogDebug("Cart cache HIT for user {UserId}", userId);
        }

        return cart;
    }

    public async Task SetAsync(Cart cart)
    {
        var key = GetCacheKey(cart.UserId, cart.TenantId);
        await _cache.SetAsync(key, cart);
        _logger.LogDebug("Cart cached for user {UserId}", cart.UserId);
    }

    public async Task InvalidateAsync(string userId, string tenantId)
    {
        var key = GetCacheKey(userId, tenantId);
        await _cache.RemoveAsync(key);
        _logger.LogDebug("Cart cache invalidated for user {UserId}", userId);
    }

    private static string GetCacheKey(string userId, string tenantId)
    {
        return $"cart:{tenantId}:{userId}";
    }
}
