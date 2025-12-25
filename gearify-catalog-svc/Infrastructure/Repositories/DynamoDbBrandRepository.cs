using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation for brand repository
/// Table: gearify-brands
/// Access Patterns:
/// 1. Get all brands (GSI1: TENANT#{tenantId}#BRANDS)
/// 2. Get brand by ID (Primary: PK + SK)
/// 3. Get brand by slug (GSI1: slug lookup)
/// </summary>
public class DynamoDbBrandRepository(IAmazonDynamoDB dynamoDb) : IBrandRepository
{
    private readonly string _tableName = "gearify-brands";

    public async Task<List<Brand>> GetAllBrandsAsync(string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#BRANDS" } }
            }
        };

        var response = await dynamoDb.QueryAsync(request);

        return response.Items
            .Select(MapToBrand)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Name)
            .ToList();
    }

    public async Task<Brand?> GetBrandByIdAsync(string brandId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#BRAND#{brandId}" } },
                { "SK", new AttributeValue { S = "METADATA" } }
            }
        };

        var response = await dynamoDb.GetItemAsync(request);

        if (response.Item == null || response.Item.Count == 0)
            return null;

        return MapToBrand(response.Item);
    }

    public async Task<Brand?> GetBrandBySlugAsync(string slug, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#BRANDS" } },
                { ":gsi1sk", new AttributeValue { S = $"BRAND#{slug}" } }
            }
        };

        var response = await dynamoDb.QueryAsync(request);

        if (response.Items.Count == 0)
            return null;

        return MapToBrand(response.Items[0]);
    }

    #region Mapping

    private Brand MapToBrand(Dictionary<string, AttributeValue> item)
    {
        return new Brand
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item["Slug"].S,
            Description = item.TryGetValue("Description", out var description) ? description.S : string.Empty,
            Logo = item.TryGetValue("Logo", out var logo) ? logo.S : string.Empty,
            Country = item.TryGetValue("Country", out var country) ? country.S : string.Empty,
            Website = item.TryGetValue("Website", out var website) ? website.S : string.Empty,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }

    #endregion
}
