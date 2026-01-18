using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of user repository
/// </summary>
public class DynamoDbUserRepository : IUserRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbUserRepository(IAmazonDynamoDB dynamoDb, IOptions<StorageConfiguration> storageConfig)
    {
        _dynamoDb = dynamoDb;
        _tableName = storageConfig.Value.DynamoDb.UsersTableName;
    }

    public async Task<User?> GetByIdAsync(string userId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"USER#{userId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return DeserializeUser(response.Item);
    }

    public async Task<User?> GetByEmailAsync(string email, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#EMAIL#{email.ToLowerInvariant()}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        if (response.Items.Count == 0)
            return null;

        return DeserializeUser(response.Items[0]);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :gsi2pk AND GSI2SK = :gsi2sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi2pk", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { ":gsi2sk", new AttributeValue { S = $"REFRESH#{refreshToken}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        if (response.Items.Count == 0)
            return null;

        return DeserializeUser(response.Items[0]);
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token)
    {
        // Scan the table to find user with this verification token
        // Note: This is inefficient but acceptable for verification token lookups which are rare
        var request = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "EmailVerificationToken = :token",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":token", new AttributeValue { S = token } }
            }
        };

        var response = await _dynamoDb.ScanAsync(request);

        if (response.Items.Count == 0)
            return null;

        return DeserializeUser(response.Items[0]);
    }

    public async Task CreateAsync(User user)
    {
        var item = SerializeUser(user);

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateAsync(User user)
    {
        user.UpdatedAt = DateTime.UtcNow;
        await CreateAsync(user); // DynamoDB PutItem acts as upsert
    }

    public async Task DeleteAsync(string userId, string tenantId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"USER#{userId}" } }
            }
        });
    }

    private Dictionary<string, AttributeValue> SerializeUser(User user)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{user.TenantId}" } },
            { "SK", new AttributeValue { S = $"USER#{user.Id}" } },
            { "GSI1PK", new AttributeValue { S = $"TENANT#{user.TenantId}#EMAIL#{user.Email.ToLowerInvariant()}" } },
            { "GSI1SK", new AttributeValue { S = $"USER#{user.Id}" } },
            { "Id", new AttributeValue { S = user.Id } },
            { "TenantId", new AttributeValue { S = user.TenantId } },
            { "Email", new AttributeValue { S = user.Email } },
            { "PasswordHash", new AttributeValue { S = user.PasswordHash } },
            { "FirstName", new AttributeValue { S = user.FirstName } },
            { "LastName", new AttributeValue { S = user.LastName } },
            { "Phone", new AttributeValue { S = user.Phone } },
            { "Role", new AttributeValue { S = user.Role } },
            { "IsActive", new AttributeValue { BOOL = user.IsActive } },
            { "EmailVerified", new AttributeValue { BOOL = user.EmailVerified } },
            { "CreatedAt", new AttributeValue { S = user.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = user.UpdatedAt.ToString("O") } }
        };

        if (user.LastLoginAt.HasValue)
        {
            item["LastLoginAt"] = new AttributeValue { S = user.LastLoginAt.Value.ToString("O") };
        }

        if (!string.IsNullOrEmpty(user.RefreshToken))
        {
            item["RefreshToken"] = new AttributeValue { S = user.RefreshToken };
            item["GSI2PK"] = new AttributeValue { S = $"TENANT#{user.TenantId}" };
            item["GSI2SK"] = new AttributeValue { S = $"REFRESH#{user.RefreshToken}" };
        }

        if (user.RefreshTokenExpiry.HasValue)
        {
            item["RefreshTokenExpiry"] = new AttributeValue { S = user.RefreshTokenExpiry.Value.ToString("O") };
        }

        if (!string.IsNullOrEmpty(user.EmailVerificationToken))
        {
            item["EmailVerificationToken"] = new AttributeValue { S = user.EmailVerificationToken };
        }

        if (user.EmailVerificationTokenExpiry.HasValue)
        {
            item["EmailVerificationTokenExpiry"] = new AttributeValue { S = user.EmailVerificationTokenExpiry.Value.ToString("O") };
        }

        // Password History
        if (!string.IsNullOrEmpty(user.PasswordHistory))
        {
            item["PasswordHistory"] = new AttributeValue { S = user.PasswordHistory };
        }

        // Password Reset Fields
        if (!string.IsNullOrEmpty(user.PasswordResetToken))
        {
            item["PasswordResetToken"] = new AttributeValue { S = user.PasswordResetToken };
        }

        if (user.PasswordResetTokenExpiry.HasValue)
        {
            item["PasswordResetTokenExpiry"] = new AttributeValue { S = user.PasswordResetTokenExpiry.Value.ToString("O") };
        }

        if (user.LastPasswordChangeAt.HasValue)
        {
            item["LastPasswordChangeAt"] = new AttributeValue { S = user.LastPasswordChangeAt.Value.ToString("O") };
        }

        // Account Lockout Fields
        item["FailedLoginAttempts"] = new AttributeValue { N = user.FailedLoginAttempts.ToString() };
        item["LockoutEnabled"] = new AttributeValue { BOOL = user.LockoutEnabled };

        if (user.LockoutEnd.HasValue)
        {
            item["LockoutEnd"] = new AttributeValue { S = user.LockoutEnd.Value.ToString("O") };
        }

        // Session Tracking
        item["ActiveSessionCount"] = new AttributeValue { N = user.ActiveSessionCount.ToString() };

        // Address Fields
        if (!string.IsNullOrEmpty(user.AddressLine1))
        {
            item["AddressLine1"] = new AttributeValue { S = user.AddressLine1 };
        }

        if (!string.IsNullOrEmpty(user.AddressLine2))
        {
            item["AddressLine2"] = new AttributeValue { S = user.AddressLine2 };
        }

        if (!string.IsNullOrEmpty(user.City))
        {
            item["City"] = new AttributeValue { S = user.City };
        }

        if (!string.IsNullOrEmpty(user.State))
        {
            item["State"] = new AttributeValue { S = user.State };
        }

        if (!string.IsNullOrEmpty(user.ZipCode))
        {
            item["ZipCode"] = new AttributeValue { S = user.ZipCode };
        }

        if (!string.IsNullOrEmpty(user.Country))
        {
            item["Country"] = new AttributeValue { S = user.Country };
        }

        return item;
    }

    private User DeserializeUser(Dictionary<string, AttributeValue> item)
    {
        var user = new User
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Email = item["Email"].S,
            PasswordHash = item["PasswordHash"].S,
            FirstName = item["FirstName"].S,
            LastName = item["LastName"].S,
            Phone = item.ContainsKey("Phone") ? item["Phone"].S : string.Empty,
            Role = item.ContainsKey("Role") ? item["Role"].S : "Customer",
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            EmailVerified = item.ContainsKey("EmailVerified") && item["EmailVerified"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };

        if (item.ContainsKey("LastLoginAt"))
        {
            user.LastLoginAt = DateTime.Parse(item["LastLoginAt"].S);
        }

        if (item.ContainsKey("RefreshToken"))
        {
            user.RefreshToken = item["RefreshToken"].S;
        }

        if (item.ContainsKey("RefreshTokenExpiry"))
        {
            user.RefreshTokenExpiry = DateTime.Parse(item["RefreshTokenExpiry"].S);
        }

        if (item.ContainsKey("EmailVerificationToken"))
        {
            user.EmailVerificationToken = item["EmailVerificationToken"].S;
        }

        if (item.ContainsKey("EmailVerificationTokenExpiry"))
        {
            user.EmailVerificationTokenExpiry = DateTime.Parse(item["EmailVerificationTokenExpiry"].S);
        }

        // Password History
        if (item.ContainsKey("PasswordHistory"))
        {
            user.PasswordHistory = item["PasswordHistory"].S;
        }

        // Password Reset Fields
        if (item.ContainsKey("PasswordResetToken"))
        {
            user.PasswordResetToken = item["PasswordResetToken"].S;
        }

        if (item.ContainsKey("PasswordResetTokenExpiry"))
        {
            user.PasswordResetTokenExpiry = DateTime.Parse(item["PasswordResetTokenExpiry"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        if (item.ContainsKey("LastPasswordChangeAt"))
        {
            user.LastPasswordChangeAt = DateTime.Parse(item["LastPasswordChangeAt"].S);
        }

        // Account Lockout Fields
        if (item.ContainsKey("FailedLoginAttempts"))
        {
            user.FailedLoginAttempts = int.Parse(item["FailedLoginAttempts"].N);
        }

        if (item.ContainsKey("LockoutEnabled"))
        {
            user.LockoutEnabled = item["LockoutEnabled"].BOOL;
        }

        if (item.ContainsKey("LockoutEnd"))
        {
            user.LockoutEnd = DateTime.Parse(item["LockoutEnd"].S, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        // Session Tracking
        if (item.ContainsKey("ActiveSessionCount"))
        {
            user.ActiveSessionCount = int.Parse(item["ActiveSessionCount"].N);
        }

        // Address Fields
        if (item.ContainsKey("AddressLine1"))
        {
            user.AddressLine1 = item["AddressLine1"].S;
        }

        if (item.ContainsKey("AddressLine2"))
        {
            user.AddressLine2 = item["AddressLine2"].S;
        }

        if (item.ContainsKey("City"))
        {
            user.City = item["City"].S;
        }

        if (item.ContainsKey("State"))
        {
            user.State = item["State"].S;
        }

        if (item.ContainsKey("ZipCode"))
        {
            user.ZipCode = item["ZipCode"].S;
        }

        if (item.ContainsKey("Country"))
        {
            user.Country = item["Country"].S;
        }

        return user;
    }
}
