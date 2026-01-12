namespace Gearify.SharedKernel.Events;

/// <summary>
/// Interface for publishing domain events to SNS topics.
/// Centralizes topic routing and publishing logic.
/// Each microservice implements this with their specific configuration.
/// </summary>
public interface ISnsEventPublisher
{
    /// <summary>
    /// Publishes a domain event to the appropriate SNS topic.
    /// Topic routing is handled internally based on event type.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}
