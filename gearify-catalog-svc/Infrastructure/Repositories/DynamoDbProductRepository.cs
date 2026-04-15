using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.CatalogService.Infrastructure.Helpers;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public class DynamoDbProductRepository(
    IAmazonDynamoDB dynamoDb,
    IOptions<StorageConfiguration> storageConfigurationOptions) : IProductRepository
{
    private readonly IAmazonDynamoDB _dynamoDb = dynamoDb;
    private readonly string _tableName = storageConfigurationOptions.Value.DynamoDb.ProductsTableName;

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
        // Use categorySlug for GSI7 lookup (convert category name to slug)
        var categorySlug = category.ToLowerInvariant().Replace(" ", "-");

        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI7",
            KeyConditionExpression = "GSI7PK = :gsi7pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi7pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categorySlug}" } }
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
        // =============================================================================
        // IMPORTANT: GSI Keys are computed automatically by GsiKeyHelper
        // You don't need to manually set these - just pass a Product with normal fields
        // =============================================================================

        // Compute GSI6 keys for featured products (sparse index - only if IsFeatured=true)
        var (gsi6PK, gsi6SK) = GsiKeyHelper.ComputeFeaturedSortKeys(product);

        // Compute GSI7 keys for category-based lookups
        var (gsi7PK, gsi7SK) = GsiKeyHelper.ComputeCategorySortKeys(product);

        var item = new Dictionary<string, AttributeValue>
        {
            // ===== Main Table Keys =====
            { "PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },

            // ===== GSI1: All Products (Default Listing) =====
            // Used when: No sort specified or sortBy=default
            { "GSI1PK", new AttributeValue { S = $"TENANT#{product.TenantId}#PRODUCTS" } },
            { "GSI1SK", new AttributeValue { S = $"PRODUCT#{product.Id}" } },

            // ===== GSI2: Price Sorting =====
            // Used when: sortBy=price-asc or sortBy=price-desc
            // SK Format: PRICE#0000129999#PRODUCT#{id} (price in cents, zero-padded)
            { "GSI2PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "GSI2SK", new AttributeValue { S = GsiKeyHelper.ComputePriceSortKey(product) } },

            // ===== GSI3: Rating Sorting =====
            // Used when: sortBy=rating (highest rated first)
            // SK Format: RATING#00450#PRODUCT#{id} (rating * 100, zero-padded)
            { "GSI3PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "GSI3SK", new AttributeValue { S = GsiKeyHelper.ComputeRatingSortKey(product) } },

            // ===== GSI4: CreatedAt Sorting =====
            // Used when: sortBy=newest (newest products first)
            // SK Format: CREATEDAT#2025-12-29T00:00:00.000Z#PRODUCT#{id}
            { "GSI4PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "GSI4SK", new AttributeValue { S = GsiKeyHelper.ComputeCreatedAtSortKey(product) } },

            // ===== GSI5: Name Sorting =====
            // Used when: sortBy=name (alphabetical A-Z)
            // SK Format: NAME#kookaburra bat#PRODUCT#{id} (lowercase for case-insensitive sort)
            { "GSI5PK", new AttributeValue { S = $"TENANT#{product.TenantId}" } },
            { "GSI5SK", new AttributeValue { S = GsiKeyHelper.ComputeNameSortKey(product) } },

            // ===== GSI7: Category Lookup =====
            // Used for: recommendations (similar/complementary), category browsing
            // PK: TENANT#{tenantId}#CATEGORY#{categorySlug}, SK: PRODUCT#{id}
            { "GSI7PK", new AttributeValue { S = gsi7PK } },
            { "GSI7SK", new AttributeValue { S = gsi7SK } },

            // Note: GSI6 (Featured Products) is added below only if IsFeatured=true

            // ===== Product Data Fields =====
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
            { "IsDeal", new AttributeValue { BOOL = product.IsDeal } },
            { "IsClearance", new AttributeValue { BOOL = product.IsClearance } },
            { "IsNewArrival", new AttributeValue { BOOL = product.IsNewArrival } },
            { "IsBestSeller", new AttributeValue { BOOL = product.IsBestSeller } },
            { "IsFeatured", new AttributeValue { BOOL = product.IsFeatured } },
            { "CreatedAt", new AttributeValue { S = product.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = product.UpdatedAt.ToString("O") } }
        };


        // ===== GSI6: Featured Products (Sparse Index) =====
        // Only add GSI6 keys if the product is featured
        if (gsi6PK != null && gsi6SK != null)
        {
            item["GSI6PK"] = new AttributeValue { S = gsi6PK };
            item["GSI6SK"] = new AttributeValue { S = gsi6SK };
        }

        // ===== Optional Fields =====
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

        if (product.DealStartDate.HasValue)
        {
            item["DealStartDate"] = new AttributeValue { S = product.DealStartDate.Value.ToString("O") };
        }

        if (product.DealEndDate.HasValue)
        {
            item["DealEndDate"] = new AttributeValue { S = product.DealEndDate.Value.ToString("O") };
        }

        // Custom collections (tenant-specific flags)
        if (product.CustomCollections.Any())
        {
            var customCollectionsMap = new Dictionary<string, AttributeValue>();
            foreach (var kvp in product.CustomCollections)
            {
                customCollectionsMap[kvp.Key] = new AttributeValue { BOOL = kvp.Value };
            }
            item["CustomCollections"] = new AttributeValue { M = customCollectionsMap };
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
            Id = item.TryGetValue("Id", out var id) ? id.S : item["PK"].S,
            TenantId = item.TryGetValue("TenantId", out var tenantId) ? tenantId.S : string.Empty,
            Sku = item.TryGetValue("Sku", out var sku) ? sku.S : string.Empty,
            Name = item.TryGetValue("Name", out var name) ? name.S : string.Empty,
            Description = item.TryGetValue("Description", out var description) ? description.S : string.Empty,
            ThumbnailUrl = item.TryGetValue("ThumbnailUrl", out var thumbnailValue) ? thumbnailValue.S : string.Empty,
            Department = item.TryGetValue("Department", out var department) ? department.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var departmentSlug) ? departmentSlug.S : string.Empty,
            Category = item.TryGetValue("Category", out var category) ? category.S : string.Empty,
            CategorySlug = item.TryGetValue("CategorySlug", out var categorySlug) ? categorySlug.S : string.Empty,
            Subcategory = item.TryGetValue("Subcategory", out var subcategory) ? subcategory.S : string.Empty,
            SubcategorySlug = item.TryGetValue("SubcategorySlug", out var subcategorySlug) ? subcategorySlug.S : string.Empty,
            Brand = item.TryGetValue("Brand", out var brand) ? brand.S : string.Empty,
            BrandSlug = item.TryGetValue("BrandSlug", out var brandSlug) ? brandSlug.S : string.Empty,
            Price = item.TryGetValue("Price", out var price) ? decimal.Parse(price.N) : 0,
            CompareAtPrice = item.TryGetValue("CompareAtPrice", out var compareAtPrice) ? decimal.Parse(compareAtPrice.N) : 0,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            DiscountPercentage = item.TryGetValue("DiscountPercentage", out var discountPct) ? decimal.Parse(discountPct.N) : null,
            OfferBadge = item.TryGetValue("OfferBadge", out var offerBadge) ? offerBadge.S : null,
            RatingAverage = item.TryGetValue("RatingAverage", out var ratingAvg) ? decimal.Parse(ratingAvg.N) : null,
            RatingCount = item.TryGetValue("RatingCount", out var ratingCnt) ? int.Parse(ratingCnt.N) : null,
            IsDeal = item.TryGetValue("IsDeal", out var isDeal) && isDeal.BOOL,
            IsClearance = item.TryGetValue("IsClearance", out var isClearance) && isClearance.BOOL,
            IsNewArrival = item.TryGetValue("IsNewArrival", out var isNewArrival) && isNewArrival.BOOL,
            IsBestSeller = item.TryGetValue("IsBestSeller", out var isBestSeller) && isBestSeller.BOOL,
            IsFeatured = item.TryGetValue("IsFeatured", out var isFeatured) && isFeatured.BOOL,
            DealStartDate = item.TryGetValue("DealStartDate", out var dealStart) ? DateTime.Parse(dealStart.S) : null,
            DealEndDate = item.TryGetValue("DealEndDate", out var dealEnd) ? DateTime.Parse(dealEnd.S) : null,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = item.TryGetValue("CreatedAt", out var createdAt) ? DateTime.Parse(createdAt.S) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAt", out var updatedAt) ? DateTime.Parse(updatedAt.S) : DateTime.UtcNow
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


        if (item.TryGetValue("CustomCollections", out var customColl) && customColl.M != null)
        {
            product.CustomCollections = customColl.M.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BOOL);
        }

        return product;
    }
}
