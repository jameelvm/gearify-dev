namespace Gearify.MediaService.Infrastructure.Configuration;

/// <summary>
/// Product upload configuration
/// </summary>
public class ProductUploadSettings
{
    /// <summary>
    /// Maximum file size in bytes (default: 10 MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed image content types
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    /// <summary>
    /// Image size settings for variant generation
    /// </summary>
    public ImageSizeSettings ImageSizes { get; set; } = new();

    /// <summary>
    /// Image quality settings for variant generation
    /// </summary>
    public ImageQualitySettings ImageQuality { get; set; } = new();
}

/// <summary>
/// Image size configurations (width/height in pixels)
/// </summary>
public class ImageSizeSettings
{
    public int ThumbnailSize { get; set; } = 150;
    public int MediumSize { get; set; } = 600;
    public int LargeSize { get; set; } = 1200;
}

/// <summary>
/// Image quality settings (0-100)
/// </summary>
public class ImageQualitySettings
{
    public int Thumbnail { get; set; } = 75;
    public int Medium { get; set; } = 85;
    public int Large { get; set; } = 90;
    public int Original { get; set; } = 100;
}

/// <summary>
/// Messaging configuration for SNS and SQS
/// </summary>
public class MessagingSettings
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

/// <summary>
/// Storage configuration for S3 bucket and DynamoDB tables
/// </summary>
public class StorageSettings
{
    public AmazonS3Settings AmazonS3 { get; set; } = new();
    public DynamoDbSettings DynamoDb { get; set; } = new();
}

/// <summary>
/// Amazon S3 storage settings
/// </summary>
public class AmazonS3Settings
{
    /// <summary>
    /// S3 bucket name for storing product images
    /// </summary>
    public string ProductImageBucketName { get; set; } = string.Empty;
}

/// <summary>
/// DynamoDB storage settings
/// </summary>
public class DynamoDbSettings
{
    /// <summary>
    /// DynamoDB table name for media metadata
    /// </summary>
    public string MediaTableName { get; set; } = string.Empty;
}
