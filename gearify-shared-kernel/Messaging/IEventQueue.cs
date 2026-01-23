using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gearify.SharedKernel.Messaging;

/// <summary>
/// Generic interface for consuming events from a message queue.
/// </summary>
public interface IEventQueue<T>
{
    Task<List<QueueMessage<T>>> ReceiveMessagesAsync(
        int maxMessages,
        int waitTimeSeconds,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default);

    Task ReturnMessageAsync(string receiptHandle, int visibilityTimeoutSeconds = 30,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
