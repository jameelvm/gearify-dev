using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gearify.CartService.Infrastructure.Caching;

/// <summary>
/// Redis implementation of cache service for Cart Service
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultExpiration;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis.GetDatabase();
        _logger = logger;
        _keyPrefix = configuration["Redis:InstanceName"] ?? "gearify-cart:";

        var expirationDays = configuration.GetValue<int>("CartConfiguration:DefaultExpirationDays", 7);
        _defaultExpiration = TimeSpan.FromDays(expirationDays);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var cachedData = await _redis.StringGetAsync(fullKey);

            if (cachedData.HasValue)
            {
                _logger.LogDebug("Cache HIT for key {Key}", key);
                return JsonSerializer.Deserialize<T>(cachedData!, _jsonOptions);
            }

            _logger.LogDebug("Cache MISS for key {Key}", key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when getting key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        try
        {
            var fullKey = GetFullKey(key);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            var ttl = expiration ?? _defaultExpiration;

            await _redis.StringSetAsync(fullKey, json, ttl);
            _logger.LogDebug("Cache SET for key {Key}, TTL: {TTL}", key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when setting key {Key}", key);
            // Don't throw - cache failures shouldn't break the operation
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            await _redis.KeyDeleteAsync(fullKey);
            _logger.LogDebug("Cache REMOVE for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when removing key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            var fullKey = GetFullKey(key);
            return await _redis.KeyExistsAsync(fullKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis error when checking key {Key}", key);
            return false;
        }
    }

    private string GetFullKey(string key)
    {
        return $"{_keyPrefix}{key}";
    }
}
