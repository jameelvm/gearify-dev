using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for getting special product collections configuration
/// Returns tenant-specific collections like Deals, Clearance, etc.
/// </summary>
public class GetSpecialCollectionsQueryHandler : IRequestHandler<GetSpecialCollectionsQuery, SpecialCollectionsResponse>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetSpecialCollectionsQueryHandler> _logger;

    public GetSpecialCollectionsQueryHandler(
        IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ITenantContext tenantContext,
        ILogger<GetSpecialCollectionsQueryHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.CatalogTableName;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SpecialCollectionsResponse> Handle(GetSpecialCollectionsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        try
        {
            // Build PK with department GUID if provided
            string pk;
            if (string.IsNullOrEmpty(request.DepartmentSlug))
            {
                pk = $"TENANT#{tenantId}";
            }
            else
            {
                // First, lookup department GUID by slug using GSI1
                var departmentQuery = new QueryRequest
                {
                    TableName = _tableName,
                    IndexName = "GSI1",
                    KeyConditionExpression = "GSI1PK = :gsi1pk AND GSI1SK = :gsi1sk",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#SLUG" } },
                        { ":gsi1sk", new AttributeValue { S = $"DEPARTMENT#{request.DepartmentSlug}" } }
                    },
                    Limit = 1
                };

                var departmentResponse = await _dynamoDb.QueryAsync(departmentQuery, cancellationToken);
                if (!departmentResponse.Items.Any())
                {
                    _logger.LogWarning("Department not found for slug {DepartmentSlug}", request.DepartmentSlug);
                    return new SpecialCollectionsResponse(new List<SpecialCollectionDto>());
                }

                var departmentId = departmentResponse.Items.First()["Id"].S;
                pk = $"TENANT#{tenantId}#DEPARTMENT#{departmentId}";
            }

            var getRequest = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = pk } },
                    { "SK", new AttributeValue { S = "SPECIAL_COLLECTIONS" } }
                }
            };

            var response = await _dynamoDb.GetItemAsync(getRequest, cancellationToken);

            if (!response.IsItemSet)
            {
                _logger.LogWarning("Special collections not found for tenant {TenantId}, department {DepartmentSlug}",
                    tenantId, request.DepartmentSlug ?? "none");
                return new SpecialCollectionsResponse(new List<SpecialCollectionDto>());
            }

            var collections = ParseCollections(response.Item);

            // Filter only active collections and sort by display order
            var activeCollections = collections
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            _logger.LogInformation("Retrieved {Count} special collections for tenant {TenantId}",
                activeCollections.Count, tenantId);

            return new SpecialCollectionsResponse(activeCollections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving special collections for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private List<SpecialCollectionDto> ParseCollections(Dictionary<string, AttributeValue> item)
    {
        var collections = new List<SpecialCollectionDto>();

        if (!item.TryGetValue("Collections", out var collectionsAttr) || collectionsAttr.L == null)
        {
            return collections;
        }

        foreach (var collectionAttr in collectionsAttr.L)
        {
            if (collectionAttr.M == null) continue;

            var collection = new SpecialCollectionDto
            {
                Id = collectionAttr.M.TryGetValue("Id", out var id) ? id.S : string.Empty,
                Slug = collectionAttr.M.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
                Label = collectionAttr.M.TryGetValue("Label", out var label) ? label.S : string.Empty,
                Icon = collectionAttr.M.TryGetValue("Icon", out var icon) ? icon.S : string.Empty,
                FilterAttribute = collectionAttr.M.TryGetValue("FilterAttribute", out var filterAttr) ? filterAttr.S : string.Empty,
                FilterType = collectionAttr.M.TryGetValue("FilterType", out var filterType) ? filterType.S : "Common",
                DisplayOrder = collectionAttr.M.TryGetValue("DisplayOrder", out var displayOrder) ? int.Parse(displayOrder.N) : 0,
                IsActive = collectionAttr.M.TryGetValue("IsActive", out var isActive) && isActive.BOOL,
                BadgeText = collectionAttr.M.TryGetValue("BadgeText", out var badgeText) ? badgeText.S : null,
                BadgeColor = collectionAttr.M.TryGetValue("BadgeColor", out var badgeColor) ? badgeColor.S : null,
                Description = collectionAttr.M.TryGetValue("Description", out var description) ? description.S : null
            };

            collections.Add(collection);
        }

        return collections;
    }
}
