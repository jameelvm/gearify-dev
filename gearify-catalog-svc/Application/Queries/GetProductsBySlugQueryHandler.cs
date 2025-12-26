using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Constants;
using Gearify.Shared.MultiTenancy;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for getting products by slug-based filters
/// </summary>
public class GetProductsBySlugQueryHandler : IRequestHandler<GetProductsBySlugQuery, ProductListResponse>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductsBySlugQueryHandler> _logger;

    public GetProductsBySlugQueryHandler(
        IAmazonDynamoDB dynamoDb,
        ITenantContext tenantContext,
        ILogger<GetProductsBySlugQueryHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductListResponse> Handle(GetProductsBySlugQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var queryRequest = new QueryRequest
        {
            TableName = DynamoDbTableNames.PRODUCTS,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#PRODUCTS" } }
            }
        };

        // Build filter expression for slugs
        var filterExpressions = new List<string>();

        if (!string.IsNullOrEmpty(request.DepartmentSlug))
        {
            filterExpressions.Add("DepartmentSlug = :deptSlug");
            queryRequest.ExpressionAttributeValues.Add(":deptSlug", new AttributeValue { S = request.DepartmentSlug });
        }

        if (!string.IsNullOrEmpty(request.CategorySlug))
        {
            filterExpressions.Add("contains(Category, :category)");
            queryRequest.ExpressionAttributeValues.Add(":category", new AttributeValue { S = request.CategorySlug });
        }

        if (!string.IsNullOrEmpty(request.SubcategorySlug))
        {
            filterExpressions.Add("contains(Tags, :subcategory)");
            queryRequest.ExpressionAttributeValues.Add(":subcategory", new AttributeValue { S = request.SubcategorySlug });
        }

        if (filterExpressions.Any())
        {
            queryRequest.FilterExpression = string.Join(" AND ", filterExpressions);
        }

        try
        {
            var response = await _dynamoDb.QueryAsync(queryRequest, cancellationToken);

            var products = response.Items.Select(MapToProduct).ToList();

            _logger.LogInformation(
                "Retrieved {Count} products for tenant {TenantId} with filters: Dept={Dept}, Cat={Cat}, Subcat={Subcat}",
                products.Count, tenantId, request.DepartmentSlug, request.CategorySlug, request.SubcategorySlug);

            return new ProductListResponse(products, products.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products by slug for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private Product MapToProduct(Dictionary<string, AttributeValue> item)
    {
        return new Product
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Description = item.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            Sku = item.TryGetValue("Sku", out var sku) ? sku.S : string.Empty,
            Price = item.TryGetValue("Price", out var price) ? decimal.Parse(price.N) : 0,
            CompareAtPrice = item.TryGetValue("CompareAtPrice", out var comparePrice) ? decimal.Parse(comparePrice.N) : 0,
            Currency = item.TryGetValue("Currency", out var currency) ? currency.S : "USD",
            DiscountPercentage = item.TryGetValue("DiscountPercentage", out var discountPct) ? decimal.Parse(discountPct.N) : null,
            OfferBadge = item.TryGetValue("OfferBadge", out var offerBadge) ? offerBadge.S : null,
            RatingAverage = item.TryGetValue("RatingAverage", out var ratingAvg) ? decimal.Parse(ratingAvg.N) : null,
            RatingCount = item.TryGetValue("RatingCount", out var ratingCnt) ? int.Parse(ratingCnt.N) : null,
            Department = item.TryGetValue("Department", out var dept) ? dept.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var deptSlug) ? deptSlug.S : string.Empty,
            Category = item.TryGetValue("Category", out var cat) ? cat.S : string.Empty,
            CategorySlug = item.TryGetValue("CategorySlug", out var catSlug) ? catSlug.S : string.Empty,
            Subcategory = item.TryGetValue("Subcategory", out var subcat) ? subcat.S : string.Empty,
            SubcategorySlug = item.TryGetValue("SubcategorySlug", out var subcatSlug) ? subcatSlug.S : string.Empty,
            Brand = item.TryGetValue("Brand", out var brand) ? brand.S : string.Empty,
            BrandSlug = item.TryGetValue("BrandSlug", out var brandSlug) ? brandSlug.S : string.Empty,
            ImageUrls = item.TryGetValue("ImageUrls", out var images) ? images.SS : new List<string>(),
            Tags = item.TryGetValue("Tags", out var tags) ? tags.SS : new List<string>(),
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = item.TryGetValue("CreatedAt", out var created) ? DateTime.Parse(created.S) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAt", out var updated) ? DateTime.Parse(updated.S) : DateTime.UtcNow,
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }
}
