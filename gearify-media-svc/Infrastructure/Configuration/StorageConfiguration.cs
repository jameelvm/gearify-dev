namespace Gearify.MediaService.Infrastructure.Configuration;

/// <summary>
/// Storage configuration for S3 bucket and DynamoDB tables
/// </summary>
public class StorageConfiguration
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