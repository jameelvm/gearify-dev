using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation for price range repository
/// Table: gearify-price-ranges
/// Access Patterns:
/// 1. Get all price ranges for tenant (PK: TENANT#{tenantId}, SK: begins_with PRICERANGE#)
/// 2. Get price ranges by category (filter on Category attribute)
/// 3. Get price range by ID (PK + SK)
/// </summary>
public class DynamoDbPriceRangeRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<StorageConfiguration> storageConfigurationOptions) : IPriceRangeRepository
{
    private readonly string _tableName = storageConfigurationOptions.Value.DynamoDb.PriceRangesTableName;

    public async Task<List<PriceRange>> GetPriceRangesAsync(string tenantId, string? category = null, bool onlyCategorySpecific = false)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            FilterExpression = "IsActive = :isActive",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { ":sk", new AttributeValue { S = "PRICERANGE#" } },
                { ":isActive", new AttributeValue { BOOL = true } }
            }
        };

        // Add category filter based on parameters
        if (!string.IsNullOrEmpty(category))
        {
            if (onlyCategorySpecific)
            {
                // Only return ranges specific to this category (exclude global)
                request.FilterExpression += " AND Category = :category";
                request.ExpressionAttributeValues.Add(":category", new AttributeValue { S = category });
            }
            else
            {
                // Return global ranges + category-specific ranges
                request.FilterExpression += " AND (attribute_not_exists(Category) OR Category = :category)";
                request.ExpressionAttributeValues.Add(":category", new AttributeValue { S = category });
            }
        }
        else
        {
            // If no category specified, only get global ranges
            request.FilterExpression += " AND attribute_not_exists(Category)";
        }

        var response = await dynamoDb.QueryAsync(request);

        return response.Items
            .Select(MapToPriceRange)
            .OrderBy(pr => pr.DisplayOrder)
            .ThenBy(pr => pr.MinPrice)
            .ToList();
    }

    public async Task<PriceRange?> GetByIdAsync(string id, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"PRICERANGE#{id}" } }
            }
        };

        var response = await dynamoDb.GetItemAsync(request);

        if (response.Item == null || response.Item.Count == 0)
            return null;

        return MapToPriceRange(response.Item);
    }

    public async Task CreateAsync(PriceRange priceRange)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{priceRange.TenantId}" } },
            { "SK", new AttributeValue { S = $"PRICERANGE#{priceRange.Id}" } },
            { "Id", new AttributeValue { S = priceRange.Id } },
            { "TenantId", new AttributeValue { S = priceRange.TenantId } },
            { "Label", new AttributeValue { S = priceRange.Label } },
            { "MinPrice", new AttributeValue { N = priceRange.MinPrice.ToString() } },
            { "Currency", new AttributeValue { S = priceRange.Currency } },
            { "DisplayOrder", new AttributeValue { N = priceRange.DisplayOrder.ToString() } },
            { "IsActive", new AttributeValue { BOOL = priceRange.IsActive } },
            { "CreatedAt", new AttributeValue { S = priceRange.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = priceRange.UpdatedAt.ToString("O") } },
            { "CreatedBy", new AttributeValue { S = priceRange.CreatedBy } },
            { "UpdatedBy", new AttributeValue { S = priceRange.UpdatedBy } }
        };

        // Add optional fields
        if (priceRange.MaxPrice.HasValue)
        {
            item["MaxPrice"] = new AttributeValue { N = priceRange.MaxPrice.Value.ToString() };
        }

        if (!string.IsNullOrEmpty(priceRange.Category))
        {
            item["Category"] = new AttributeValue { S = priceRange.Category };
        }

        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateAsync(PriceRange priceRange)
    {
        priceRange.UpdatedAt = DateTime.UtcNow;
        await CreateAsync(priceRange); // DynamoDB PutItem acts as upsert
    }

    public async Task DeleteAsync(string id, string tenantId)
    {
        await dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"PRICERANGE#{id}" } }
            }
        });
    }

    private PriceRange MapToPriceRange(Dictionary<string, AttributeValue> item)
    {
        return new PriceRange
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Label = item["Label"].S,
            MinPrice = decimal.Parse(item["MinPrice"].N),
            MaxPrice = item.TryGetValue("MaxPrice", out var maxPrice) ? decimal.Parse(maxPrice.N) : null,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
            Category = item.TryGetValue("Category", out var category) ? category.S : null,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }
}
