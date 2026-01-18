using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.AuthService.Domain.Entities;
using Gearify.AuthService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation of address repository
/// </summary>
public class DynamoDbAddressRepository : IAddressRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    public DynamoDbAddressRepository(IAmazonDynamoDB dynamoDb, IOptions<StorageConfiguration> storageConfig)
    {
        _dynamoDb = dynamoDb;
        _tableName = storageConfig.Value.DynamoDb.UsersTableName;
    }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(string userId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#USER#{userId}" } },
                { ":skPrefix", new AttributeValue { S = "ADDRESS#" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeAddress).OrderByDescending(a => a.IsDefault).ThenBy(a => a.Label);
    }

    public async Task<Address?> GetByIdAsync(string addressId, string userId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#USER#{userId}" } },
                { "SK", new AttributeValue { S = $"ADDRESS#{addressId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return DeserializeAddress(response.Item);
    }

    public async Task<Address?> GetDefaultAsync(string userId, string tenantId)
    {
        var addresses = await GetByUserIdAsync(userId, tenantId);
        return addresses.FirstOrDefault(a => a.IsDefault);
    }

    public async Task CreateAsync(Address address)
    {
        var item = SerializeAddress(address);

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateAsync(Address address)
    {
        address.UpdatedAt = DateTime.UtcNow;
        await CreateAsync(address);
    }

    public async Task DeleteAsync(string addressId, string userId, string tenantId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#USER#{userId}" } },
                { "SK", new AttributeValue { S = $"ADDRESS#{addressId}" } }
            }
        });
    }

    public async Task ClearDefaultAsync(string userId, string tenantId)
    {
        var addresses = await GetByUserIdAsync(userId, tenantId);

        foreach (var address in addresses.Where(a => a.IsDefault))
        {
            address.IsDefault = false;
            await UpdateAsync(address);
        }
    }

    private Dictionary<string, AttributeValue> SerializeAddress(Address address)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{address.TenantId}#USER#{address.UserId}" } },
            { "SK", new AttributeValue { S = $"ADDRESS#{address.Id}" } },
            { "Id", new AttributeValue { S = address.Id } },
            { "UserId", new AttributeValue { S = address.UserId } },
            { "TenantId", new AttributeValue { S = address.TenantId } },
            { "Label", new AttributeValue { S = address.Label } },
            { "FirstName", new AttributeValue { S = address.FirstName } },
            { "LastName", new AttributeValue { S = address.LastName } },
            { "Phone", new AttributeValue { S = address.Phone } },
            { "AddressLine1", new AttributeValue { S = address.AddressLine1 } },
            { "City", new AttributeValue { S = address.City } },
            { "State", new AttributeValue { S = address.State } },
            { "ZipCode", new AttributeValue { S = address.ZipCode } },
            { "Country", new AttributeValue { S = address.Country } },
            { "IsDefault", new AttributeValue { BOOL = address.IsDefault } },
            { "CreatedAt", new AttributeValue { S = address.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = address.UpdatedAt.ToString("O") } },
            { "EntityType", new AttributeValue { S = "Address" } }
        };

        if (!string.IsNullOrEmpty(address.AddressLine2))
        {
            item["AddressLine2"] = new AttributeValue { S = address.AddressLine2 };
        }

        return item;
    }

    private Address DeserializeAddress(Dictionary<string, AttributeValue> item)
    {
        return new Address
        {
            Id = item["Id"].S,
            UserId = item["UserId"].S,
            TenantId = item["TenantId"].S,
            Label = item["Label"].S,
            FirstName = item["FirstName"].S,
            LastName = item["LastName"].S,
            Phone = item.TryGetValue("Phone", out var phoneAttr) ? phoneAttr.S : string.Empty,
            AddressLine1 = item["AddressLine1"].S,
            AddressLine2 = item.TryGetValue("AddressLine2", out var addressLine2Attr) ? addressLine2Attr.S : null,
            City = item["City"].S,
            State = item["State"].S,
            ZipCode = item["ZipCode"].S,
            Country = item["Country"].S,
            IsDefault = item.TryGetValue("IsDefault", out var isDefaultAttr) && isDefaultAttr.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };
    }
}
