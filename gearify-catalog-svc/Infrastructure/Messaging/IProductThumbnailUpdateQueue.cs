using Gearify.CatalogService.Infrastructure.Messaging.Models;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// Queue for receiving image processing completion events from Media Service
/// </summary>
public interface IProductThumbnailUpdateQueue
{
    Task<List<QueueMessage<ImageProcessingCompletedEvent>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default);
}
