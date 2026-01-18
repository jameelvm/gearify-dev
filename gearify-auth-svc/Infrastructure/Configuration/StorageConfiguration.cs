namespace Gearify.AuthService.Infrastructure.Configuration;

public class StorageConfiguration
{
    public DynamoDbSettings DynamoDb { get; set; } = new();
}

public class DynamoDbSettings
{
    public string UsersTableName { get; set; } = "gearify-users";
    public string SessionsTableName { get; set; } = "UserSessions";
}
