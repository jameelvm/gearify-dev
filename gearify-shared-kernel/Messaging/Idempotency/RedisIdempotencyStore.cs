using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Gearify.SharedKernel.Messaging.Idempotency;

/// <summary>
/// Redis-based implementation of IIdempotencyStore.
/// Uses Redis SET with NX (not exists) for atomic claim operations.
/// Supports automatic TTL-based cleanup.
/// </summary>
public class RedisIdempotencyStore : IIdempotencyStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotencyStore> _logger;
    private readonly TimeSpan _ttl;
    private readonly string _keyPrefix;

    /// <summary>
    /// Creates a new Redis idempotency store.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer</param>
    /// <param name="logger">Logger</param>
    /// <param name="ttl">Time-to-live for processed event records (default: 7 days)</param>
    /// <param name="keyPrefix">Prefix for Redis keys (default: "idempotency:")</param>
    public RedisIdempotencyStore(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotencyStore> logger,
        TimeSpan? ttl = null,
        string keyPrefix = "idempotency:")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ttl = ttl ?? TimeSpan.FromDays(7);
        _keyPrefix = keyPrefix;
    }

    public async Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventId))
            return false;

        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(eventId);
            var exists = await db.KeyExistsAsync(key);

            if (exists)
            {
                _logger.LogDebug("Event {EventId} has already been processed", eventId);
            }

            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if event {EventId} has been processed", eventId);
            throw;
        }
    }

    public async Task MarkAsProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventId))
            return;

        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(eventId);

            // Set with TTL - will overwrite if exists (extending TTL)
            await db.StringSetAsync(key, DateTime.UtcNow.ToString("O"), _ttl);

            _logger.LogDebug("Marked event {EventId} as processed (TTL: {TTL})", eventId, _ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking event {EventId} as processed", eventId);
            throw;
        }
    }

    public async Task<bool> TryClaimEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventId))
            return true; // No event ID = allow processing (can't check)

        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(eventId);

            // SET key value EX seconds NX
            // NX = Only set if NOT EXISTS (atomic check-and-set)
            // Returns true if key was set (we claimed it)
            // Returns false if key already exists (someone else claimed it)
            var claimed = await db.StringSetAsync(
                key,
                DateTime.UtcNow.ToString("O"),
                _ttl,
                when: When.NotExists);

            _logger.LogDebug(
                claimed ? "Claimed event {EventId} for processing" : "Event {EventId} already claimed/processed",
                eventId);

            return claimed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming event {EventId}", eventId);
            // On Redis failure, we have two options:
            // 1. Fail closed (throw) - safer, prevents duplicates but may cause delays
            // 2. Fail open (return true) - riskier, allows duplicates but maintains throughput
            // We choose fail closed for safety
            throw;
        }
    }

    public async Task ReleaseClaimAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(eventId))
            return;

        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(eventId);
            await db.KeyDeleteAsync(key);

            _logger.LogDebug("Released claim for event {EventId} (will be retried)", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing claim for event {EventId}", eventId);
            // Don't throw - releasing the claim is best-effort.
            // If it fails, the claim will expire via TTL eventually.
        }
    }

    private string GetKey(string eventId) => $"{_keyPrefix}{eventId}";
}
