namespace Gearify.CartService.Infrastructure.Configuration;

public class StorageConfiguration
{
    public DynamoDbSettings DynamoDb { get; set; } = new();
}

public class DynamoDbSettings
{
    public string CartTableName { get; set; } = "gearify-carts";
}
