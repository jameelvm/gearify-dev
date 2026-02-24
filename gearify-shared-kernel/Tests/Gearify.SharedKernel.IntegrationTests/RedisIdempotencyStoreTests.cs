using FluentAssertions;
using Gearify.SharedKernel.IntegrationTests.Fixtures;
using Gearify.SharedKernel.Messaging.Idempotency;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gearify.SharedKernel.IntegrationTests;

[Collection("Redis")]
public class RedisIdempotencyStoreTests
{
    private readonly RedisFixture _redis;

    public RedisIdempotencyStoreTests(RedisFixture redis)
    {
        _redis = redis;
    }

    [Fact]
    public async Task TryClaimEvent_FirstCall_ReturnsTrue()
    {
        var store = CreateStore();
        var eventId = Guid.NewGuid().ToString();

        var result = await store.TryClaimEventAsync(eventId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryClaimEvent_SecondCall_ReturnsFalse()
    {
        var store = CreateStore();
        var eventId = Guid.NewGuid().ToString();

        var first = await store.TryClaimEventAsync(eventId);
        var second = await store.TryClaimEventAsync(eventId);

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseClaim_AllowsReclaim()
    {
        var store = CreateStore();
        var eventId = Guid.NewGuid().ToString();

        await store.TryClaimEventAsync(eventId);
        await store.ReleaseClaimAsync(eventId);
        var reclaimed = await store.TryClaimEventAsync(eventId);

        reclaimed.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsProcessed_MakesHasBeenProcessedTrue()
    {
        var store = CreateStore();
        var eventId = Guid.NewGuid().ToString();

        var beforeProcessed = await store.HasBeenProcessedAsync(eventId);
        await store.MarkAsProcessedAsync(eventId);
        var afterProcessed = await store.HasBeenProcessedAsync(eventId);

        beforeProcessed.Should().BeFalse();
        afterProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsProcessed_PreventsClaimAfter()
    {
        var store = CreateStore();
        var eventId = Guid.NewGuid().ToString();

        await store.MarkAsProcessedAsync(eventId);
        var claimResult = await store.TryClaimEventAsync(eventId);

        claimResult.Should().BeFalse();
    }

    private RedisIdempotencyStore CreateStore() => new(
        _redis.Connection,
        NullLogger<RedisIdempotencyStore>.Instance,
        ttl: TimeSpan.FromMinutes(5),
        keyPrefix: $"test-idemp-{Guid.NewGuid():N}:");
}
