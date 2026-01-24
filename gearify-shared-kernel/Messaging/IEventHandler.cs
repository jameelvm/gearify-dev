using System.Threading;
using System.Threading.Tasks;

namespace Gearify.SharedKernel.Messaging;

/// <summary>
/// Handles a single event message received from a queue.
/// Return true to delete the message (processed), false to leave it for retry.
/// </summary>
public interface IEventHandler<T>
{
    Task<bool> HandleAsync(T message, CancellationToken cancellationToken = default);
}
