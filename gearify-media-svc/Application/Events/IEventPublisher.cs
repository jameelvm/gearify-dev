namespace Gearify.MediaService.Application.Events;

/// <summary>
/// Abstraction for publishing events
/// Can be implemented with SNS, RabbitMQ, or in-memory for testing
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T eventData, CancellationToken cancellationToken = default) where T : class;
}
