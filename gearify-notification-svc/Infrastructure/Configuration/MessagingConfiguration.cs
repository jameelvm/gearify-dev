namespace Gearify.NotificationService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SqsConfiguration SQS { get; set; } = new();
}

public class SqsConfiguration
{
    /// <summary>
    /// Queue URL for receiving payment events from Payment Service
    /// </summary>
    public string PaymentEventsQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Queue URL for receiving refund events (RefundCompleted, RefundFailed)
    /// </summary>
    public string RefundEventsQueueUrl { get; set; } = string.Empty;
}
