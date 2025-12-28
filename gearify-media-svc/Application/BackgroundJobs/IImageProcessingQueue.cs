using Gearify.MediaService.Application.BackgroundJobs.Models;

namespace Gearify.MediaService.Application.BackgroundJobs;

/// <summary>
/// Abstraction for image processing queue
/// Can be implemented with SQS, RabbitMQ, or HTTP client (when extracted to microservice)
/// </summary>
public interface IImageProcessingQueue
{
    /// <summary>
    /// Receive messages from the queue
    /// </summary>
    Task<List<QueueMessage<ImageProcessingMessage>>> ReceiveMessagesAsync(
        int maxMessages = 10,
        int waitTimeSeconds = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a message from the queue after processing
    /// </summary>
    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Return a message to the queue (processing failed, retry later)
    /// </summary>
    Task ReturnMessageAsync(string receiptHandle, int visibilityTimeoutSeconds = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// Queue message wrapper with receipt handle for deletion
/// </summary>
public class QueueMessage<T>
{
    public T Body { get; set; } = default!;
    public string ReceiptHandle { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}
