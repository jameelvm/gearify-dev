namespace Gearify.PaymentService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SnsConfiguration
{
    public string PaymentEventsTopicArn { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}

public class SqsConfiguration
{
    /// <summary>
    /// Queue URL for receiving OrderCreatedEvent (triggers payment processing)
    /// </summary>
    public string OrderCreatedQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Queue URL for receiving OrderCancelledEvent (triggers refund processing)
    /// </summary>
    public string OrderCancelledQueueUrl { get; set; } = string.Empty;

    public int PollingIntervalSeconds { get; set; } = 5;
    public int MaxNumberOfMessages { get; set; } = 10;
}
