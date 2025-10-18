using System;
using System.Text.Json;
using System.Threading.Tasks;
using Gearify.PaymentService.Application.Commands;
using StackExchange.Redis;

namespace Gearify.PaymentService.Infrastructure.PaymentProviders;

public class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<ProcessPaymentResult?> GetResultAsync(string key)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"idempotency:{key}");

        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<ProcessPaymentResult>(value!);
    }

    public async Task SaveResultAsync(string key, ProcessPaymentResult result, TimeSpan expiration)
    {
        var db = _redis.GetDatabase();
        var value = JsonSerializer.Serialize(result);
        await db.StringSetAsync($"idempotency:{key}", value, expiration);
    }
}
