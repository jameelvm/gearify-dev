namespace Gearify.SharedKernel.Messaging.Idempotency;

/// <summary>
/// No-operation implementation of IIdempotencyStore that always allows processing.
/// Use this when idempotency checking is not needed or for backwards compatibility.
/// WARNING: This provides NO duplicate protection.
/// </summary>
public class NoOpIdempotencyStore : IIdempotencyStore
{
    public Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        // Always return false - never seen before
        return Task.FromResult(false);
    }

    public Task MarkAsProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        // Do nothing
        return Task.CompletedTask;
    }

    public Task<bool> TryClaimEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        // Always return true - always claim successfully
        return Task.FromResult(true);
    }

    public Task ReleaseClaimAsync(string eventId, CancellationToken cancellationToken = default)
    {
        // Do nothing
        return Task.CompletedTask;
    }
}
