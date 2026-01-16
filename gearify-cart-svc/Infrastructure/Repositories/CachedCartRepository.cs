using System;
using System.Text.Json;
using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gearify.CartService.Infrastructure.Repositories;

/// <summary>
/// Write-through caching repository: Redis (cache) + DynamoDB (persistent)
/// - Reads: Redis first, falls back to DynamoDB
/// - Writes: Both Redis and DynamoDB (write-through)
/// </summary>
public class CachedCartRepository : ICartRepository
{
    private readonly IDatabase _redis;
    private readonly DynamoDbCartRepository _dynamoRepository;
    private readonly ILogger<CachedCartRepository> _logger;
    private readonly TimeSpan _cacheExpiration;
    private readonly string _keyPrefix;

    public CachedCartRepository(
        IConnectionMultiplexer redis,
        DynamoDbCartRepository dynamoRepository,
        IConfiguration configuration,
        ILogger<CachedCartRepository> logger)
    {
        _redis = redis.GetDatabase();
        _dynamoRepository = dynamoRepository;
        _logger = logger;
        _keyPrefix = configuration["Redis:InstanceName"] ?? "gearify-cart:";

        var expirationDays = configuration.GetValue<int>("CartConfiguration:DefaultExpirationDays", 7);
        _cacheExpiration = TimeSpan.FromDays(expirationDays);
    }

    public async Task<Cart?> GetCartAsync(string userId, string tenantId)
    {
        var cacheKey = GetCacheKey(userId, tenantId);

        try
        {
            // Try Redis first
            var cachedData = await _redis.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                _logger.LogDebug("Cache HIT for cart {UserId}", userId);
                return JsonSerializer.Deserialize<Cart>(cachedData!);
            }

            _logger.LogDebug("Cache MISS for cart {UserId}, fetching from DynamoDB", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when getting cart {UserId}, falling back to DynamoDB", userId);
        }

        // Fall back to DynamoDB
        var cart = await _dynamoRepository.GetCartAsync(userId, tenantId);

        // Populate cache if found
        if (cart != null)
        {
            await SetCacheAsync(cacheKey, cart);
        }

        return cart;
    }

    public async Task SaveCartAsync(Cart cart)
    {
        var cacheKey = GetCacheKey(cart.UserId, cart.TenantId);

        // Write-through: Save to both stores
        // Save to DynamoDB first (source of truth)
        await _dynamoRepository.SaveCartAsync(cart);

        // Then update cache
        await SetCacheAsync(cacheKey, cart);

        _logger.LogDebug("Saved cart {CartId} to DynamoDB and Redis", cart.Id);
    }

    public async Task DeleteCartAsync(string userId, string tenantId)
    {
        var cacheKey = GetCacheKey(userId, tenantId);

        // Delete from both stores
        await _dynamoRepository.DeleteCartAsync(userId, tenantId);

        try
        {
            await _redis.KeyDeleteAsync(cacheKey);
            _logger.LogDebug("Deleted cart from DynamoDB and Redis for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when deleting cart cache for {UserId}", userId);
        }
    }

    private string GetCacheKey(string userId, string tenantId)
    {
        return $"{_keyPrefix}{tenantId}:{userId}";
    }

    private async Task SetCacheAsync(string cacheKey, Cart cart)
    {
        try
        {
            var json = JsonSerializer.Serialize(cart);
            await _redis.StringSetAsync(cacheKey, json, _cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache for cart {CartId}", cart.Id);
            // Don't throw - cache failures shouldn't break the operation
        }
    }
}
