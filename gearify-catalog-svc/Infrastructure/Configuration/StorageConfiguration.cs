namespace Gearify.CatalogService.Infrastructure.Configuration;

public class StorageConfiguration
{
    public DynamoDbSettings DynamoDb { get; set; } = new();
}

/// <summary>
/// DynamoDB storage settings
/// </summary>
public class DynamoDbSettings
{
    /// <summary>
    /// DynamoDB table name for catalog data
    /// </summary>
    public string CatalogTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for product data
    /// </summary>
    public string ProductsTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for brand data
    /// </summary>
    public string BrandsTableName { get; set; } = string.Empty;

    /// <summary>
    /// DynamoDB table name for price range data
    /// </summary>
    public string PriceRangesTableName { get; set; } = string.Empty;
}