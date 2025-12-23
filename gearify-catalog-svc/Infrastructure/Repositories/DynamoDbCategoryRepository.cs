using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation using single-table design
/// Table: gearify-catalog
/// Entities: CATEGORY, CATEGORY_SECTION, SUBCATEGORY
/// </summary>
public class DynamoDbCategoryRepository(IAmazonDynamoDB dynamoDb) : ICategoryRepository
{
    private readonly IAmazonDynamoDB _dynamoDb = dynamoDb;
    private readonly string _tableName = "gearify-catalog";

    #region Category Operations

    public async Task<Category?> GetCategoryByIdAsync(string categoryId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { "SK", new AttributeValue { S = "METADATA" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return DeserializeCategory(response.Item);
    }

    public async Task<Category?> GetCategoryBySlugAsync(string slug, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#SLUG" } },
                { ":gsi1sk", new AttributeValue { S = $"CATEGORY#{slug}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        if (response.Items.Count == 0)
            return null;

        return DeserializeCategory(response.Items[0]);
    }

    public async Task<List<Category>> GetAllCategoriesAsync(string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi2pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORIES" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeCategory).OrderBy(c => c.DisplayOrder).ToList();
    }

    public async Task CreateCategoryAsync(Category category)
    {
        var item = SerializeCategory(category);
        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateCategoryAsync(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        await CreateCategoryAsync(category); // DynamoDB PutItem acts as upsert
    }

    public async Task DeleteCategoryAsync(string categoryId, string tenantId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { "SK", new AttributeValue { S = "METADATA" } }
            }
        });
    }

    #endregion

    #region Section Operations

    public async Task<CategorySection?> GetSectionByIdAsync(string sectionId, string categoryId, string tenantId)
    {
        var request = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { "SK", new AttributeValue { S = $"SECTION#{sectionId}" } }
            }
        };

        var response = await _dynamoDb.GetItemAsync(request);

        if (!response.IsItemSet)
            return null;

        return DeserializeSection(response.Item);
    }

    public async Task<List<CategorySection>> GetSectionsByCategoryAsync(string categoryId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { ":sk", new AttributeValue { S = "SECTION#" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeSection).OrderBy(s => s.DisplayOrder).ToList();
    }

    public async Task CreateSectionAsync(CategorySection section)
    {
        var item = SerializeSection(section);
        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateSectionAsync(CategorySection section)
    {
        section.UpdatedAt = DateTime.UtcNow;
        await CreateSectionAsync(section);
    }

    public async Task DeleteSectionAsync(string sectionId, string categoryId, string tenantId)
    {
        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { "SK", new AttributeValue { S = $"SECTION#{sectionId}" } }
            }
        });
    }

    #endregion

    #region Subcategory Operations

    public async Task<Subcategory?> GetSubcategoryByIdAsync(string subcategoryId, string categoryId, string tenantId)
    {
        // Scan to find the subcategory (since we don't know the section ID)
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            FilterExpression = "Id = :id",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { ":sk", new AttributeValue { S = "SECTION#" } },
                { ":id", new AttributeValue { S = subcategoryId } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        if (response.Items.Count == 0)
            return null;

        return DeserializeSubcategory(response.Items[0]);
    }

    public async Task<List<Subcategory>> GetSubcategoriesBySectionAsync(string sectionId, string categoryId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { ":sk", new AttributeValue { S = $"SECTION#{sectionId}#ITEM#" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);
        return response.Items.Select(DeserializeSubcategory).OrderBy(s => s.DisplayOrder).ToList();
    }

    public async Task CreateSubcategoryAsync(Subcategory subcategory)
    {
        var item = SerializeSubcategory(subcategory);
        await _dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task UpdateSubcategoryAsync(Subcategory subcategory)
    {
        subcategory.UpdatedAt = DateTime.UtcNow;
        await CreateSubcategoryAsync(subcategory);
    }

    public async Task DeleteSubcategoryAsync(string subcategoryId, string categoryId, string tenantId)
    {
        // First, find the subcategory to get the section ID
        var subcategory = await GetSubcategoryByIdAsync(subcategoryId, categoryId, tenantId);
        if (subcategory == null) return;

        await _dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                { "PK", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } },
                { "SK", new AttributeValue { S = $"SECTION#{subcategory.SectionId}#ITEM#{subcategoryId}" } }
            }
        });
    }

    #endregion

    #region Get Complete Category

    public async Task<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
        GetCategoryWithDetailsAsync(string categoryId, string tenantId)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORY#{categoryId}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        Category? category = null;
        var sections = new List<CategorySection>();
        var subcategories = new List<Subcategory>();

        foreach (var item in response.Items)
        {
            var sk = item["SK"].S;
            if (sk == "METADATA")
            {
                category = DeserializeCategory(item);
            }
            else if (sk.StartsWith("SECTION#") && !sk.Contains("#ITEM#"))
            {
                sections.Add(DeserializeSection(item));
            }
            else if (sk.Contains("#ITEM#"))
            {
                subcategories.Add(DeserializeSubcategory(item));
            }
        }

        return (category ?? new Category(),
                sections.OrderBy(s => s.DisplayOrder).ToList(),
                subcategories.OrderBy(s => s.DisplayOrder).ToList());
    }

    #endregion

    #region Serialization/Deserialization

    private Dictionary<string, AttributeValue> SerializeCategory(Category category)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{category.TenantId}#CATEGORY#{category.Id}" } },
            { "SK", new AttributeValue { S = "METADATA" } },
            { "EntityType", new AttributeValue { S = "CATEGORY" } },
            { "Id", new AttributeValue { S = category.Id } },
            { "TenantId", new AttributeValue { S = category.TenantId } },
            { "Name", new AttributeValue { S = category.Name } },
            { "Slug", new AttributeValue { S = category.Slug } },
            { "Description", new AttributeValue { S = category.Description } },
            { "Icon", new AttributeValue { S = category.Icon } },
            { "ImageUrl", new AttributeValue { S = category.ImageUrl } },
            { "DisplayOrder", new AttributeValue { N = category.DisplayOrder.ToString() } },
            { "IsActive", new AttributeValue { BOOL = category.IsActive } },
            { "CreatedAt", new AttributeValue { S = category.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = category.UpdatedAt.ToString("O") } },
            { "CreatedBy", new AttributeValue { S = category.CreatedBy } },
            { "UpdatedBy", new AttributeValue { S = category.UpdatedBy } },

            // GSI1 for slug lookup
            { "GSI1PK", new AttributeValue { S = $"TENANT#{category.TenantId}#SLUG" } },
            { "GSI1SK", new AttributeValue { S = $"CATEGORY#{category.Slug}" } },

            // GSI2 for getting all categories
            { "GSI2PK", new AttributeValue { S = $"TENANT#{category.TenantId}#CATEGORIES" } },
            { "GSI2SK", new AttributeValue { S = $"ORDER#{category.DisplayOrder:D4}" } }
        };

        return item;
    }

    private Category DeserializeCategory(Dictionary<string, AttributeValue> item)
    {
        return new Category
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item["Slug"].S,
            Description = item.ContainsKey("Description") ? item["Description"].S : string.Empty,
            Icon = item.ContainsKey("Icon") ? item["Icon"].S : string.Empty,
            ImageUrl = item.ContainsKey("ImageUrl") ? item["ImageUrl"].S : string.Empty,
            DisplayOrder = item.ContainsKey("DisplayOrder") ? int.Parse(item["DisplayOrder"].N) : 0,
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.ContainsKey("CreatedBy") ? item["CreatedBy"].S : string.Empty,
            UpdatedBy = item.ContainsKey("UpdatedBy") ? item["UpdatedBy"].S : string.Empty
        };
    }

    private Dictionary<string, AttributeValue> SerializeSection(CategorySection section)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{section.TenantId}#CATEGORY#{section.CategoryId}" } },
            { "SK", new AttributeValue { S = $"SECTION#{section.Id}" } },
            { "EntityType", new AttributeValue { S = "CATEGORY_SECTION" } },
            { "Id", new AttributeValue { S = section.Id } },
            { "CategoryId", new AttributeValue { S = section.CategoryId } },
            { "TenantId", new AttributeValue { S = section.TenantId } },
            { "Title", new AttributeValue { S = section.Title } },
            { "Slug", new AttributeValue { S = section.Slug } },
            { "ShowTitle", new AttributeValue { BOOL = section.ShowTitle } },
            { "DisplayOrder", new AttributeValue { N = section.DisplayOrder.ToString() } },
            { "IsActive", new AttributeValue { BOOL = section.IsActive } },
            { "CreatedAt", new AttributeValue { S = section.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = section.UpdatedAt.ToString("O") } },
            { "CreatedBy", new AttributeValue { S = section.CreatedBy } },
            { "UpdatedBy", new AttributeValue { S = section.UpdatedBy } }
        };
    }

    private CategorySection DeserializeSection(Dictionary<string, AttributeValue> item)
    {
        return new CategorySection
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            TenantId = item["TenantId"].S,
            Title = item["Title"].S,
            Slug = item.ContainsKey("Slug") ? item["Slug"].S : string.Empty,
            ShowTitle = item.ContainsKey("ShowTitle") ? item["ShowTitle"].BOOL : true,
            DisplayOrder = item.ContainsKey("DisplayOrder") ? int.Parse(item["DisplayOrder"].N) : 0,
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.ContainsKey("CreatedBy") ? item["CreatedBy"].S : string.Empty,
            UpdatedBy = item.ContainsKey("UpdatedBy") ? item["UpdatedBy"].S : string.Empty
        };
    }

    private Dictionary<string, AttributeValue> SerializeSubcategory(Subcategory subcategory)
    {
        return new Dictionary<string, AttributeValue>
        {
            { "PK", new AttributeValue { S = $"TENANT#{subcategory.TenantId}#CATEGORY#{subcategory.CategoryId}" } },
            { "SK", new AttributeValue { S = $"SECTION#{subcategory.SectionId}#ITEM#{subcategory.Id}" } },
            { "EntityType", new AttributeValue { S = "SUBCATEGORY" } },
            { "Id", new AttributeValue { S = subcategory.Id } },
            { "CategoryId", new AttributeValue { S = subcategory.CategoryId } },
            { "SectionId", new AttributeValue { S = subcategory.SectionId } },
            { "TenantId", new AttributeValue { S = subcategory.TenantId } },
            { "Name", new AttributeValue { S = subcategory.Name } },
            { "Slug", new AttributeValue { S = subcategory.Slug } },
            { "Description", new AttributeValue { S = subcategory.Description } },
            { "ImageUrl", new AttributeValue { S = subcategory.ImageUrl } },
            { "DisplayOrder", new AttributeValue { N = subcategory.DisplayOrder.ToString() } },
            { "ProductCount", new AttributeValue { N = subcategory.ProductCount.ToString() } },
            { "IsActive", new AttributeValue { BOOL = subcategory.IsActive } },
            { "CreatedAt", new AttributeValue { S = subcategory.CreatedAt.ToString("O") } },
            { "UpdatedAt", new AttributeValue { S = subcategory.UpdatedAt.ToString("O") } },
            { "CreatedBy", new AttributeValue { S = subcategory.CreatedBy } },
            { "UpdatedBy", new AttributeValue { S = subcategory.UpdatedBy } }
        };
    }

    private Subcategory DeserializeSubcategory(Dictionary<string, AttributeValue> item)
    {
        return new Subcategory
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            SectionId = item["SectionId"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item.ContainsKey("Slug") ? item["Slug"].S : string.Empty,
            Description = item.ContainsKey("Description") ? item["Description"].S : string.Empty,
            ImageUrl = item.ContainsKey("ImageUrl") ? item["ImageUrl"].S : string.Empty,
            DisplayOrder = item.ContainsKey("DisplayOrder") ? int.Parse(item["DisplayOrder"].N) : 0,
            ProductCount = item.ContainsKey("ProductCount") ? int.Parse(item["ProductCount"].N) : 0,
            IsActive = item.ContainsKey("IsActive") && item["IsActive"].BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.ContainsKey("CreatedBy") ? item["CreatedBy"].S : string.Empty,
            UpdatedBy = item.ContainsKey("UpdatedBy") ? item["UpdatedBy"].S : string.Empty
        };
    }

    #endregion
}
