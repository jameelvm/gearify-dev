namespace Gearify.SharedKernel.Events;

/// <summary>
/// Marker interface for all domain events.
/// Events are pure data containers - no behavior.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the event occurred
    /// </summary>
    DateTime OccurredAt { get; }
}
