using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public class DynamoDbProductRepository : IProductRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName = "gearify-products";

    public DynamoDbProductRepository(IAmazonDynamoDB dynamoDb)
    {
        _dynamoDb = dynamoDb;
    }

    public async Task<Product?> GetByIdAsync(string productId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"PRODUCT#{productId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return DeserializeProduct(response.Item);
    }

    public async Task<List<Product>> GetAllAsync(string tenantId, int skip = 0, int take = 50)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { ":sk", new AttributeValue { S = "PRODUCT#" } }
            },
            Limit = take
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeProduct).ToList();
    }

    public async Task<List<Product>> GetByCategoryAsync(string category, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{category}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeProduct).ToList();
    }

    public async Task CreateAsync(Product product)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },
            { "GSI1PK", new AttributeValue { S = $"TENANT#{product.TenantId}#CATEGORY#{product.Category}" } },
            { "GSI1SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },
            { "Id", new AttributeValue { S = product.Id } },
            { "TenantId", new AttributeValue { S = product.TenantId } },
            { "Sku", new AttributeValue { S = product.Sku } },
            { "Name", new AttributeValue { S = product.Name } },
            { "Description", new AttributeValue { S = product.Description } },
            { "Category", new AttributeValue { S = product.Category } },
            { "Brand", new AttributeValue { S = product.Brand } },
            { "Price", new AttributeValue { N = product.Price.ToString() } },
            { "CompareAtPrice", new AttributeValue { N = product.CompareAtPrice.ToString() } },
            { "Currency", new AttributeValue { S = product.Currency } },
            { "IsActive", new AttributeValue { BOOL = product.IsActive } },
            { "CreatedAt", new AttributeValue { S = product.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = product.UpdatedAt.ToString("O") } }
        };

        if (product.Tags.Any())
        {
            item["Tags"] = new AttributeValue { SS = product.Tags };
        }

        if (product.ImageUrls.Any())
        {
            item["ImageUrls"] = new AttributeValue { SS = product.ImageUrls };
        }

        if (product.Attributes.Any())
        {
            item["Attributes"] = new AttributeValue { S = JsonSerializer.Serialize(product.Attributes) };
        }

        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        await CreateAsync(product); // DynamoDB PutItem acts as upsert
    }

    public async Task DeleteAsync(string productId, string tenantId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { "SK", new AttributeValue { S = $"PRODUCT#{productId}" } }
            }
        });
    }

    private Product DeserializeProduct(Dictionary<string, AttributeValue> item)
    {
        var product = new Product
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Sku = item["Sku"].S,
            Name = item["Name"].S,
            Description = item.ContainsKey("Description") ? item["Description"].S : string.Empty,
            Category = item["Category"].S,
            Brand = item.ContainsKey("Brand") ? item["Brand"].S : string.Empty,
            Price = decimal.Parse(item["Price"].N),
            CompareAtPrice = item.ContainsKey("CompareAtPrice") ? decimal.Parse(item["CompareAtPrice"].N) : 0,
            Currency = item.ContainsKey("Currency") ? item["Currency"].S : "USD",
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };

        if (item.ContainsKey("Tags") && item["Tags"].SS.Any())
        {
            product.Tags = item["Tags"].SS.ToList();
        }

        if (item.ContainsKey("ImageUrls") && item["ImageUrls"].SS.Any())
        {
            product.ImageUrls = item["ImageUrls"].SS.ToList();
        }

        if (item.ContainsKey("Attributes") && !string.IsNullOrEmpty(item["Attributes"].S))
        {
            product.Attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(item["Attributes"].S) ?? new();
        }

        return product;
    }
}
