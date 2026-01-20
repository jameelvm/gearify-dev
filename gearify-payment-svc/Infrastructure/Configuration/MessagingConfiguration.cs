namespace Gearify.PaymentService.Infrastructure.Configuration;

public class MessagingConfiguration
{
    public SnsConfiguration SNS { get; set; } = new();
    public SqsConfiguration SQS { get; set; } = new();
}

public class SnsConfiguration
{
    public string PaymentEventsTopicArn { get; set; } = string.Empty;
}

public class SqsConfiguration
{
    public string CheckoutInitiatedQueueUrl { get; set; } = string.Empty;
}
