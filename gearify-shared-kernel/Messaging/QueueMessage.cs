namespace Gearify.SharedKernel.Messaging;

/// <summary>
/// Wrapper for SQS queue messages
/// </summary>
public class QueueMessage<T>
{
    public string MessageId { get; set; } = string.Empty;
    public string ReceiptHandle { get; set; } = string.Empty;
    public T Body { get; set; } = default!;
}
