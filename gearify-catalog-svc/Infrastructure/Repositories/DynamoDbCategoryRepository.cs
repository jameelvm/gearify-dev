using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation optimized for mega-menu data retrieval
/// Table: gearify-catalog
/// Uses single-table design with parallel queries for optimal performance
/// </summary>
public class DynamoDbCategoryRepository(IAmazonDynamoDB dynamoDb) : ICategoryRepository
{
    private readonly string _tableName = "gearify-catalog";

    #region Mega Menu Operations

    public async Task<List<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>>
        GetAllCategoriesWithDetailsAsync(string tenantId)
    {
        // First, get all category IDs using GSI2
        var categoriesRequest = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi2pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORIES" } }
            }
        };

        var categoriesResponse = await dynamoDb.QueryAsync(categoriesRequest);
        var categoryIds = categoriesResponse.Items
            .Select(item => item["Id"].S)
            .ToList();

        // Fetch details for all categories in parallel
        var detailsTasks = categoryIds
            .Select(categoryId => GetCategoryWithDetailsAsync(categoryId, tenantId))
            .ToList();

        var results = await Task.WhenAll(detailsTasks);

        return results
            .Where(r => r.category is { Id: not null })
            .OrderBy(r => r.category.DisplayOrder)
            .ToList();
    }

    private async Task<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
        GetCategoryWithDetailsAsync(string categoryId, string tenantId)
    {
        // First, find the category to get its department slug
        var categoryRequest = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "Id = :id AND TenantId = :tenantId AND EntityType = :entityType",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":id", new AttributeValue { S = categoryId } },
                { ":tenantId", new AttributeValue { S = tenantId } },
                { ":entityType", new AttributeValue { S = "CATEGORY" } }
            },
            Limit = 1
        };

        var categoryResponse = await dynamoDb.ScanAsync(categoryRequest);
        if (!categoryResponse.Items.Any())
            return (new Category(), new List<CategorySection>(), new List<Subcategory>());

        var categoryItem = categoryResponse.Items.First();
        var category = MapToCategory(categoryItem);
        var departmentSlug = categoryItem.TryGetValue("DepartmentSlug", out var deptSlug) ? deptSlug.S : string.Empty;

        if (string.IsNullOrEmpty(departmentSlug))
            return (category, new List<CategorySection>(), new List<Subcategory>());

        // Now query for sections and subcategories using the new PK pattern
        var detailsRequest = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}" } }
            }
        };

        var response = await dynamoDb.QueryAsync(detailsRequest);

        var sections = new List<CategorySection>();
        var subcategories = new List<Subcategory>();

        foreach (var item in response.Items)
        {
            var sk = item["SK"].S;
            if (sk.StartsWith("SECTION#") && !sk.Contains("#ITEM#"))
            {
                sections.Add(MapToSection(item));
            }
            else if (sk.Contains("#ITEM#"))
            {
                subcategories.Add(MapToSubcategory(item));
            }
        }

        return (category,
                sections.OrderBy(s => s.DisplayOrder).ToList(),
                subcategories.OrderBy(s => s.DisplayOrder).ToList());
    }

    #endregion

    #region Mapping

    private Category MapToCategory(Dictionary<string, AttributeValue> item)
    {
        return new Category
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            DepartmentId = item.TryGetValue("DepartmentId", out var deptId) ? deptId.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var deptSlug) ? deptSlug.S : string.Empty,
            Name = item["Name"].S,
            Slug = item["Slug"].S,
            Description = item.TryGetValue("Description", out var descriptionValue) ? descriptionValue.S : string.Empty,
            Icon = item.TryGetValue("Icon", out var iconValue) ? iconValue.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var imageUrlValue) ? imageUrlValue.S : string.Empty,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrderValue) ? int.Parse(displayOrderValue.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdByValue) ? createdByValue.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedByValue) ? updatedByValue.S : string.Empty
        };
    }

    private CategorySection MapToSection(Dictionary<string, AttributeValue> item)
    {
        return new CategorySection
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            TenantId = item["TenantId"].S,
            Title = item["Title"].S,
            Slug = item.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
            ShowTitle = !item.TryGetValue("ShowTitle", out var titleValue) || titleValue.BOOL,
            Mapping = item.TryGetValue("Mapping", out var mappingValue) ? mappingValue.S : null,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrderValue) ? int.Parse(displayOrderValue.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdByValue) ? createdByValue.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedByValue) ? updatedByValue.S : string.Empty
        };
    }

    private Subcategory MapToSubcategory(Dictionary<string, AttributeValue> item)
    {
        return new Subcategory
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            SectionId = item["SectionId"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item.TryGetValue("Slug", out var slugValue) ? slugValue.S : string.Empty,
            Description = item.TryGetValue("Description", out var descriptionValue) ? descriptionValue.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var imageValue) ? imageValue.S : string.Empty,
            BrandId = item.TryGetValue("BrandId", out var brandIdValue) ? brandIdValue.S : null,
            FilterType = item.TryGetValue("FilterType", out var filterTypeValue) ? filterTypeValue.S : null,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrderValue) ? int.Parse(displayOrderValue.N) : 0,
            ProductCount = item.TryGetValue("ProductCount", out var productCountValue) ? int.Parse(productCountValue.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdByValue) ? createdByValue.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedByValue) ? updatedByValue.S : string.Empty
        };
    }

    #endregion
}
