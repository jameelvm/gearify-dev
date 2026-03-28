using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gearify.SharedKernel.AI.Caching;

public class RedisCacheService : IAICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private const string KeyPrefix = "ai:";

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(KeyPrefix + key);

            if (value.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(KeyPrefix + key, json, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyPrefix + key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for key {Key}", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan expiry, CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await factory();
        await SetAsync(key, value, expiry, cancellationToken);
        return value;
    }
}
