using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for getting products by slug-based filters
/// </summary>
public class GetProductsBySlugQueryHandler : IRequestHandler<GetProductsBySlugQuery, ProductListResponse>
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductsBySlugQueryHandler> _logger;

    public GetProductsBySlugQueryHandler(
        IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ITenantContext tenantContext,
        ILogger<GetProductsBySlugQueryHandler> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.ProductsTableName;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductListResponse> Handle(GetProductsBySlugQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        var queryRequest = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "GSI1",
            KeyConditionExpression = "GSI1PK = :gsi1pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":gsi1pk", new AttributeValue { S = $"TENANT#{tenantId}#PRODUCTS" } }
            }
        };

        var filterExpressions = await Build(request, cancellationToken, queryRequest, tenantId);

        if (filterExpressions.Any())
        {
            queryRequest.FilterExpression = string.Join(" AND ", filterExpressions);
        }

        try
        {
            var response = await _dynamoDb.QueryAsync(queryRequest, cancellationToken);

            var products = response.Items.Select(MapToProduct).ToList();

            // Apply sorting if specified
            if (!string.IsNullOrEmpty(request.SortBy))
            {
                products = request.SortBy.ToLower() switch
                {
                    "price-asc" or "price: low to high" => products.OrderBy(p => p.Price).ToList(),
                    "price-desc" or "price: high to low" => products.OrderByDescending(p => p.Price).ToList(),
                    "rating" or "top rated" => products.OrderByDescending(p => p.RatingAverage ?? 0).ToList(),
                    "newest" or "newest first" => products.OrderByDescending(p => p.CreatedAt).ToList(),
                    "name" or "featured items" => products.OrderBy(p => p.Name).ToList(),
                    _ => products // Default: no sorting
                };
            }

            var productDtos = products.Select(ProductListDto.FromProduct).ToList();

            _logger.LogInformation(
                "Retrieved {Count} products for tenant {TenantId} with filters: Dept={Dept}, Cat={Cat}, Subcat={Subcat}, Brands={Brands}, SortBy={SortBy}",
                products.Count, tenantId, request.DepartmentSlug, request.CategorySlug, request.SubcategorySlug,
                request.BrandSlugs != null ? string.Join(",", request.BrandSlugs) : "none", request.SortBy ?? "none");

            return new ProductListResponse(productDtos, productDtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products by slug for tenant {TenantId}", tenantId);
            throw;
        }
    }

    private async Task<List<string>> Build(GetProductsBySlugQuery request, CancellationToken cancellationToken, QueryRequest queryRequest,
        string tenantId)
    {
        // Build filter expression for slugs
        var filterExpressions = new List<string>();

        if (!string.IsNullOrEmpty(request.DepartmentSlug))
        {
            filterExpressions.Add("DepartmentSlug = :deptSlug");
            queryRequest.ExpressionAttributeValues.Add(":deptSlug", new AttributeValue { S = request.DepartmentSlug });
        }

        if (!string.IsNullOrEmpty(request.CategorySlug))
        {
            filterExpressions.Add("CategorySlug = :catSlug");
            queryRequest.ExpressionAttributeValues.Add(":catSlug", new AttributeValue { S = request.CategorySlug });
        }

        if (!string.IsNullOrEmpty(request.SubcategorySlug))
        {
            filterExpressions.Add("SubcategorySlug = :subcatSlug");
            queryRequest.ExpressionAttributeValues.Add(":subcatSlug", new AttributeValue { S = request.SubcategorySlug });
        }

        // Handle multiple brands with IN operator
        if (request.BrandSlugs is { Length: > 0 })
        {
            if (request.BrandSlugs.Length == 1)
            {
                // Single brand - use equality for better performance
                filterExpressions.Add("BrandSlug = :brand0");
                queryRequest.ExpressionAttributeValues.Add(":brand0", new AttributeValue { S = request.BrandSlugs[0] });
            }
            else
            {
                // Multiple brands - use IN operator
                var brandPlaceholders = new List<string>();
                for (int i = 0; i < request.BrandSlugs.Length; i++)
                {
                    var placeholder = $":brand{i}";
                    brandPlaceholders.Add(placeholder);
                    queryRequest.ExpressionAttributeValues.Add(placeholder, new AttributeValue { S = request.BrandSlugs[i] });
                }
                filterExpressions.Add($"BrandSlug IN ({string.Join(", ", brandPlaceholders)})");
            }
        }

        if (request.MinPrice.HasValue)
        {
            filterExpressions.Add("Price >= :minPrice");
            queryRequest.ExpressionAttributeValues.Add(":minPrice", new AttributeValue { N = request.MinPrice.Value.ToString() });
        }

        if (request.MaxPrice.HasValue)
        {
            filterExpressions.Add("Price <= :maxPrice");
            queryRequest.ExpressionAttributeValues.Add(":maxPrice", new AttributeValue { N = request.MaxPrice.Value.ToString() });
        }

        // Filter by special collection (deals, clearance, etc.)
        if (!string.IsNullOrEmpty(request.CollectionId))
        {
            var collection = await GetCollectionConfig(request.CollectionId, cancellationToken);

            if (collection != null)
            {
                if (collection.FilterType == "Common")
                {
                    // Filter on top-level boolean field (optimized for performance)
                    filterExpressions.Add($"{collection.FilterAttribute} = :collectionValue");
                }
                else // Custom
                {
                    // Filter on CustomCollections map
                    filterExpressions.Add($"CustomCollections.{collection.FilterAttribute} = :collectionValue");
                }

                queryRequest.ExpressionAttributeValues.Add(":collectionValue", new AttributeValue { BOOL = true });
            }
            else
            {
                _logger.LogWarning("Collection {CollectionId} not found for tenant {TenantId}", request.CollectionId, tenantId);
            }
        }

        return filterExpressions;
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
            ThumbnailUrl = item.TryGetValue("ThumbnailUrl", out var thumbnailUrl) ? thumbnailUrl.S : null,
            ImageUrls = item.TryGetValue("ImageUrls", out var images) ? images.SS : new List<string>(),
            Tags = item.TryGetValue("Tags", out var tags) ? tags.SS : new List<string>(),
            // Product attributes
            IsDeal = item.TryGetValue("IsDeal", out var isDeal) && isDeal.BOOL,
            IsClearance = item.TryGetValue("IsClearance", out var isClearance) && isClearance.BOOL,
            IsNewArrival = item.TryGetValue("IsNewArrival", out var isNewArrival) && isNewArrival.BOOL,
            IsBestSeller = item.TryGetValue("IsBestSeller", out var isBestSeller) && isBestSeller.BOOL,
            IsFeatured = item.TryGetValue("IsFeatured", out var isFeatured) && isFeatured.BOOL,
            DealStartDate = item.TryGetValue("DealStartDate", out var dealStart) ? DateTime.Parse(dealStart.S) : null,
            DealEndDate = item.TryGetValue("DealEndDate", out var dealEnd) ? DateTime.Parse(dealEnd.S) : null,
            CustomCollections = item.TryGetValue("CustomCollections", out var customColl) && customColl.M != null
                ? customColl.M.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.BOOL)
                : new Dictionary<string, bool>(),
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = item.TryGetValue("CreatedAt", out var created) ? DateTime.Parse(created.S) : DateTime.UtcNow,
            UpdatedAt = item.TryGetValue("UpdatedAt", out var updated) ? DateTime.Parse(updated.S) : DateTime.UtcNow,
            CreatedBy = item.TryGetValue("CreatedBy", out var createdBy) ? createdBy.S : string.Empty,
            UpdatedBy = item.TryGetValue("UpdatedBy", out var updatedBy) ? updatedBy.S : string.Empty
        };
    }

    private async Task<SpecialCollectionDto?> GetCollectionConfig(string collectionId, CancellationToken cancellationToken)
    {
        try
        {
            var getRequest = new GetItemRequest
            {
                TableName = _tableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    { "PK", new AttributeValue { S = $"TENANT#{_tenantContext.TenantId}" } },
                    { "SK", new AttributeValue { S = "SPECIAL_COLLECTIONS" } }
                }
            };

            var response = await _dynamoDb.GetItemAsync(getRequest, cancellationToken);

            if (!response.IsItemSet || !response.Item.TryGetValue("Collections", out var collectionsAttr))
            {
                return null;
            }

            // Find the matching collection by ID
            foreach (var collectionAttr in collectionsAttr.L)
            {
                if (collectionAttr.M == null) continue;

                // Compare by Slug (not Id) since frontend passes slug as collectionId
                if (collectionAttr.M.TryGetValue("Slug", out var slug) && slug.S == collectionId)
                {
                    return new SpecialCollectionDto
                    {
                        Id = collectionAttr.M.TryGetValue("Id", out var id) ? id.S : string.Empty,
                        FilterAttribute = collectionAttr.M.TryGetValue("FilterAttribute", out var filterAttr) ? filterAttr.S : string.Empty,
                        FilterType = collectionAttr.M.TryGetValue("FilterType", out var filterType) ? filterType.S : "Common"
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching collection config for {CollectionId}", collectionId);
            return null;
        }
    }
}
