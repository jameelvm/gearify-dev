namespace Gearify.SearchService.Infrastructure.Configuration;

/// <summary>
/// Configuration for messaging (SQS/SNS)
/// </summary>
public class MessagingSettings
{
    public SqsSettings SQS { get; set; } = new();
}

public class SqsSettings
{
    /// <summary>
    /// URL of the SQS queue to poll for catalog events
    /// </summary>
    public string CatalogEventsQueueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of messages to receive per poll (1-10)
    /// </summary>
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>
    /// Long polling wait time in seconds (0-20)
    /// </summary>
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>
    /// Visibility timeout in seconds (how long a message is hidden after being received)
    /// </summary>
    public int VisibilityTimeoutSeconds { get; set; } = 30;
}
