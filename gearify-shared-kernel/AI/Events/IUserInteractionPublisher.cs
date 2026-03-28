namespace Gearify.SharedKernel.AI.Events;

public interface IUserInteractionPublisher
{
    Task PublishAsync(UserInteractionEvent evt, CancellationToken cancellationToken = default);
}
