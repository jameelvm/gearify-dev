namespace Gearify.MediaService.Infrastructure.Configuration;

/// <summary>
/// Messaging configuration for SNS and SQS
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
    /// <summary>
    /// Topic ARN for MediaUploadedEvent (published after original upload)
    /// </summary>
    public string MediaUploadedTopicArn { get; set; } = string.Empty;

    /// <summary>
    /// Topic ARN for ImageProcessingCompletedEvent (published after variant generation)
    /// </summary>
    public string ImageProcessingCompletedTopicArn { get; set; } = string.Empty;
}

/// <summary>
/// SQS queue URL configuration
/// </summary>
public class SqsSettings
{
    /// <summary>
    /// Queue URL for receiving image processing jobs
    /// </summary>
    public string ImageProcessingQueueUrl { get; set; } = string.Empty;
}
