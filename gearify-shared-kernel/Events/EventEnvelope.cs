using System;

namespace Gearify.SharedKernel.Events;

/// <summary>
/// Standard envelope for all domain events published via SNS.
/// Provides consistent metadata for routing, filtering, and idempotency.
/// </summary>
public class EventEnvelope
{
    /// <summary>
    /// Unique identifier for this event instance (for idempotency)
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// Type name of the event (e.g., "OrderCreatedEvent", "PaymentFailedEvent")
    /// Used for filtering and routing
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Tenant identifier for multi-tenancy support
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The actual event data
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Creates an envelope wrapping the given domain event
    /// </summary>
    public static EventEnvelope Wrap<TEvent>(TEvent domainEvent, string tenantId) where TEvent : IDomainEvent
    {
        return new EventEnvelope
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = typeof(TEvent).Name,
            TenantId = tenantId,
            Timestamp = domainEvent.OccurredAt,
            Payload = domainEvent
        };
    }
}
