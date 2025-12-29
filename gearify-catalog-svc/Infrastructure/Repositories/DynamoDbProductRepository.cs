using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public class DynamoDbProductRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<CatalogDataSettings> catalogDataSettings) : IProductRepository
{
    private readonly IAmazonDynamoDB _dynamoDb = dynamoDb;
    private readonly string _tableName = catalogDataSettings.Value.ProductsTableName;

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
        try
        {
            Console.WriteLine($"[DynamoDbProductRepository] Querying products for tenant: {tenantId}");
            Console.WriteLine($"[DynamoDbProductRepository] Table name: {_tableName}");
            Console.WriteLine($"[DynamoDbProductRepository] DynamoDB client endpoint: {_dynamoDb.Config.ServiceURL}");

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

            Console.WriteLine($"[DynamoDbProductRepository] Executing query...");
            var response = await _dynamoDb.QueryAsync(request);
            Console.WriteLine($"[DynamoDbProductRepository] Query successful. Items returned: {response.Items.Count}");

            return response.Items.Select(DeserializeProduct).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DynamoDbProductRepository] ERROR: {ex.GetType().Name}");
            Console.WriteLine($"[DynamoDbProductRepository] Message: {ex.Message}");
            Console.WriteLine($"[DynamoDbProductRepository] Stack trace: {ex.StackTrace}");
            throw;
        }
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

    public async Task<int> GetProductCountByBrandAsync(string brandId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            FilterExpression = "Brand = :brandId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}" } },
                { ":sk", new AttributeValue { S = "PRODUCT#" } },
                { ":brandId", new AttributeValue { S = brandId } }
            },
            Select = "COUNT"
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Count;
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
            { "Department", new AttributeValue { S = product.Department } },
            { "DepartmentSlug", new AttributeValue { S = product.DepartmentSlug } },
            { "Category", new AttributeValue { S = product.Category } },
            { "CategorySlug", new AttributeValue { S = product.CategorySlug } },
            { "Subcategory", new AttributeValue { S = product.Subcategory } },
            { "SubcategorySlug", new AttributeValue { S = product.SubcategorySlug } },
            { "Brand", new AttributeValue { S = product.Brand } },
            { "BrandSlug", new AttributeValue { S = product.BrandSlug } },
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

        if (!string.IsNullOrEmpty(product.ThumbnailUrl))
        {
            item["ThumbnailUrl"] = new AttributeValue { S = product.ThumbnailUrl };
        }

        if (product.Attributes.Any())
        {
            item["Attributes"] = new AttributeValue { S = JsonSerializer.Serialize(product.Attributes) };
        }

        // Optional discount/offer fields
        if (product.DiscountPercentage.HasValue)
        {
            item["DiscountPercentage"] = new AttributeValue { N = product.DiscountPercentage.Value.ToString() };
        }

        if (!string.IsNullOrEmpty(product.OfferBadge))
        {
            item["OfferBadge"] = new AttributeValue { S = product.OfferBadge };
        }

        if (product.RatingAverage.HasValue)
        {
            item["RatingAverage"] = new AttributeValue { N = product.RatingAverage.Value.ToString() };
        }

        if (product.RatingCount.HasValue)
        {
            item["RatingCount"] = new AttributeValue { N = product.RatingCount.Value.ToString() };
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
            Description = item.TryGetValue("Description", out var description) ? description.S : string.Empty,
            ThumbnailUrl = item.TryGetValue("ThumbnailUrl", out var thumbnailValue) ? thumbnailValue.S : string.Empty,
            Department = item.TryGetValue("Department", out var department) ? department.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var departmentSlug) ? departmentSlug.S : string.Empty,
            Category = item["Category"].S,
            CategorySlug = item.TryGetValue("CategorySlug", out var categorySlug) ? categorySlug.S : string.Empty,
            Subcategory = item.TryGetValue("Subcategory", out var subcategory) ? subcategory.S : string.Empty,
            SubcategorySlug = item.TryGetValue("SubcategorySlug", out var subcategorySlug) ? subcategorySlug.S : string.Empty,
            Brand = item.TryGetValue("Brand", out var brand) ? brand.S : string.Empty,
            BrandSlug = item.TryGetValue("BrandSlug", out var brandSlug) ? brandSlug.S : string.Empty,
            Price = decimal.Parse(item["Price"].N),
            CompareAtPrice = item.TryGetValue("CompareAtPrice", out var compareAtPrice) ? decimal.Parse(compareAtPrice.N) : 0,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            DiscountPercentage = item.TryGetValue("DiscountPercentage", out var discountPct) ? decimal.Parse(discountPct.N) : null,
            OfferBadge = item.TryGetValue("OfferBadge", out var offerBadge) ? offerBadge.S : null,
            RatingAverage = item.TryGetValue("RatingAverage", out var ratingAvg) ? decimal.Parse(ratingAvg.N) : null,
            RatingCount = item.TryGetValue("RatingCount", out var ratingCnt) ? int.Parse(ratingCnt.N) : null,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };

        if (item.TryGetValue("Tags", out var tags) && tags.SS.Any())
        {
            product.Tags = tags.SS.ToList();
        }

        if (item.TryGetValue("ImageUrls", out var imageUrls) && imageUrls.SS.Any())
        {
            product.ImageUrls = imageUrls.SS.ToList();
        }

        if (item.TryGetValue("Attributes", out var attributes) && !string.IsNullOrEmpty(attributes.S))
        {
            product.Attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(attributes.S) ?? new();
        }

        return product;
    }
}
