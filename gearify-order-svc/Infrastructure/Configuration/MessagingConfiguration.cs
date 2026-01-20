namespace Gearify.OrderService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SnsConfiguration
{
    public string OrderEventsTopicArn { get; set; } = string.Empty;
}

public class SqsConfiguration
{
    public string PaymentCompletedQueueUrl { get; set; } = string.Empty;
    public string PaymentFailedQueueUrl { get; set; } = string.Empty;
    public string ShippingCreatedQueueUrl { get; set; } = string.Empty;
    public string ShippingStatusQueueUrl { get; set; } = string.Empty;
}
