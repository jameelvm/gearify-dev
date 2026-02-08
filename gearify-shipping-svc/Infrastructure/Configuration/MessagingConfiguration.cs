namespace Gearify.ShippingService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SnsConfiguration
{
    public string ShippingEventsTopicArn { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}

public class SqsConfiguration
{
    /// <summary>
    /// Queue URL for receiving OrderConfirmedEvent
    /// </summary>
    public string OrderConfirmedQueueUrl { get; set; } = string.Empty;
}
