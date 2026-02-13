using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gearify.SharedKernel.Messaging.Idempotency;

/// <summary>
/// Extension methods for registering idempotency services.
/// </summary>
public static class IdempotencyExtensions
{
    /// <summary>
    /// Adds Redis-based idempotency store to the service collection.
    /// Requires IConnectionMultiplexer to be registered.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="ttl">Time-to-live for processed event records (default: 7 days)</param>
    /// <param name="keyPrefix">Prefix for Redis keys (default: "idempotency:")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddRedisIdempotency(
        this IServiceCollection services,
        TimeSpan? ttl = null,
        string keyPrefix = "idempotency:")
    {
        services.AddSingleton<IIdempotencyStore>(sp =>
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisIdempotencyStore>>();
            return new RedisIdempotencyStore(redis, logger, ttl, keyPrefix);
        });

        return services;
    }

    /// <summary>
    /// Adds Redis-based idempotency store using a connection string.
    /// Creates and registers the IConnectionMultiplexer.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="redisConnectionString">Redis connection string</param>
    /// <param name="ttl">Time-to-live for processed event records (default: 7 days)</param>
    /// <param name="keyPrefix">Prefix for Redis keys (default: "idempotency:")</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddRedisIdempotency(
        this IServiceCollection services,
        string redisConnectionString,
        TimeSpan? ttl = null,
        string keyPrefix = "idempotency:")
    {
        // Register Redis connection if not already registered
        services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(redisConnectionString));

        return services.AddRedisIdempotency(ttl, keyPrefix);
    }

    /// <summary>
    /// Adds a no-op idempotency store (no duplicate checking).
    /// Use only when idempotency is not required or for testing.
    /// </summary>
    public static IServiceCollection AddNoOpIdempotency(this IServiceCollection services)
    {
        services.AddSingleton<IIdempotencyStore, NoOpIdempotencyStore>();
        return services;
    }
}
