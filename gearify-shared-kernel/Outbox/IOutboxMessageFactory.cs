using Gearify.SharedKernel.Events;

namespace Gearify.SharedKernel.Outbox;

/// <summary>
/// Creates OutboxMessage instances from domain events.
/// Resolves TopicArn and serializes the event envelope at write-time
/// so the OutboxPublisher needs zero routing knowledge.
/// </summary>
public interface IOutboxMessageFactory
{
    OutboxMessage CreateOutboxMessage<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
}
