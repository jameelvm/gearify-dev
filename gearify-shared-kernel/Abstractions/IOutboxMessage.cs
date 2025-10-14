namespace Gearify.SharedKernel.Abstractions;

public interface IOutboxMessage
{
    string Id { get; }
    string EventType { get; }
    string Payload { get; }
    DateTime OccurredAt { get; }
}
