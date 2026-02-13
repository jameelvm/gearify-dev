namespace Gearify.SharedKernel.Messaging.Idempotency;

/// <summary>
/// Store for tracking processed event IDs to enable idempotent message handling.
/// Prevents duplicate processing when SQS delivers the same message multiple times.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Checks if an event has already been processed.
    /// </summary>
    /// <param name="eventId">The unique event identifier from EventEnvelope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the event was already processed, false otherwise</returns>
    Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an event as processed. Should be called after successful handling.
    /// </summary>
    /// <param name="eventId">The unique event identifier from EventEnvelope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkAsProcessedAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks if an event has been processed and claims it if not.
    /// This prevents race conditions when multiple workers receive the same message.
    /// </summary>
    /// <param name="eventId">The unique event identifier from EventEnvelope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if this call successfully claimed the event (should process),
    /// false if already claimed/processed (should skip)</returns>
    Task<bool> TryClaimEventAsync(string eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously claimed event so it can be retried.
    /// Call this when processing fails to avoid silent message loss.
    /// </summary>
    /// <param name="eventId">The unique event identifier from EventEnvelope</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReleaseClaimAsync(string eventId, CancellationToken cancellationToken = default);
}
