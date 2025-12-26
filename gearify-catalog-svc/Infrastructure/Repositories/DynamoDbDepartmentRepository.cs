using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Constants;

namespace Gearify.CatalogService.Infrastructure.Repositories;

/// <summary>
/// DynamoDB implementation for Department repository
/// Table: gearify-catalog
/// PK Pattern: TENANT#{tenantId}#DEPARTMENT#{departmentSlug}
/// SK Pattern: METADATA
/// </summary>
public class DynamoDbDepartmentRepository(IAmazonDynamoDB dynamoDb) : IDepartmentRepository
{
    private readonly string _tableName = DynamoDbTableNames.CATALOG;

    public async Task<List<Department>> GetAllAsync(string tenantId)
    {
        // Query all departments for this tenant using begins_with on PK
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi2pk", new AttributeValue { S = $"TENANT#{tenantId}#DEPARTMENTS" } }
            }
        };

        var response = await dynamoDb.QueryAsync(request);
        return response.Items
            .Select(MapToDepartment)
            .OrderBy(d => d.DisplayOrder)
            .ToList();
    }

    public async Task<Department?> GetByIdAsync(string departmentId, string tenantId)
    {
        // Scan to find department by ID (less efficient, but ID lookups are rare)
        var request = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = "Id = :id AND TenantId = :tenantId AND EntityType = :entityType",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":id", new AttributeValue { S = departmentId } },
                { ":tenantId", new AttributeValue { S = tenantId } },
                { ":entityType", new AttributeValue { S = "DEPARTMENT" } }
            }
        };

        var response = await dynamoDb.ScanAsync(request);
        return response.Items.FirstOrDefault() != null
            ? MapToDepartment(response.Items.First())
            : null;
    }

    public async Task<Department?> GetBySlugAsync(string slug, string tenantId)
    {
        // Direct query using PK
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND SK = :sk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"TENANT#{tenantId}#DEPARTMENT#{slug}" } },
                { ":sk", new AttributeValue { S = "METADATA" } }
            }
        };

        var response = await dynamoDb.QueryAsync(request);
        return response.Items.FirstOrDefault() != null
            ? MapToDepartment(response.Items.First())
            : null;
    }

    public async Task<List<Category>> GetCategoriesAsync(string departmentSlug, string tenantId)
    {
        // Query all categories using GSI2, then filter by department
        var request = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI2",
            KeyConditionExpression = "GSI2PK = :gsi2pk",
            FilterExpression = "DepartmentSlug = :deptSlug AND EntityType = :entityType",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi2pk", new AttributeValue { S = $"TENANT#{tenantId}#CATEGORIES" } },
                { ":deptSlug", new AttributeValue { S = departmentSlug } },
                { ":entityType", new AttributeValue { S = "CATEGORY" } }
            }
        };

        var response = await dynamoDb.QueryAsync(request);
        return response.Items
            .Select(MapToCategory)
            .OrderBy(c => c.DisplayOrder)
            .ToList();
    }

    #region Mapping

    private Department MapToDepartment(Dictionary<string, AttributeValue> item)
    {
        return new Department
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item["Slug"].S,
            Description = item.TryGetValue("Description", out var description) ? description.S : string.Empty,
            Icon = item.TryGetValue("Icon", out var icon) ? icon.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var imageUrl) ? imageUrl.S : string.Empty,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S),
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }

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
            Description = item.TryGetValue("Description", out var description) ? description.S : string.Empty,
            Icon = item.TryGetValue("Icon", out var icon) ? icon.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var imageUrl) ? imageUrl.S : string.Empty,
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
