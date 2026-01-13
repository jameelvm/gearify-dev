namespace Gearify.CatalogService.Infrastructure.Configuration;

/// <summary>
/// Messaging configuration for SQS
/// </summary>
public class MessagingConfiguration
{
    public SnsSettings SNS { get; set; } = new();
    public SqsSettings SQS { get; set; } = new();
}

/// <summary>
/// SNS topic ARN configuration
/// </summary>
public class SnsSettings
{
    public string CatalogEventsTopicArn { get; set; } = string.Empty;
}

/// <summary>
/// SQS queue URL configuration
/// </summary>
public class SqsSettings
{
    /// <summary>
    /// Queue URL for receiving product thumbnail update events
    /// </summary>
    public string ProductThumbnailUpdateQueueUrl { get; set; } = string.Empty;
}


