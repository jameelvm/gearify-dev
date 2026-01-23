namespace Gearify.OrderService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SnsConfiguration
{
    public string OrderEventsTopicArn { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}

public class SqsConfiguration
{
    /// <summary>
    /// Queue URL for receiving payment result events from Payment Service
    /// </summary>
    public string PaymentProcessedQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Polling interval in seconds
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of messages to receive per poll
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;
}
