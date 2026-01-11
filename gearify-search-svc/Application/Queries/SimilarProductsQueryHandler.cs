using Gearify.SearchService.Application.DTOs;
using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Gearify.SearchService.Infrastructure.OpenSearch;
using MediatR;
using Microsoft.Extensions.Logging;
using Nest;

namespace Gearify.SearchService.Application.Queries;

public class SimilarProductsQueryHandler : IRequestHandler<SimilarProductsQuery, SimilarProductsResponse>
{
    private readonly IElasticClient _client;
    private readonly IIndexManager _indexManager;
    private readonly ILogger<SimilarProductsQueryHandler> _logger;

    // Scoring weights - can be tuned or made configurable for AI recommendations
    private const double CategoryBoost = 3.0;
    private const double BrandBoost = 2.0;
    private const double PriceRangeBoost = 1.5;
    private const double TagsBoost = 1.0;

    public SimilarProductsQueryHandler(
        IOpenSearchClientFactory clientFactory,
        IIndexManager indexManager,
        ILogger<SimilarProductsQueryHandler> logger)
    {
        _client = clientFactory.CreateClient();
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task<SimilarProductsResponse> Handle(SimilarProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? "default-tenant";
        var indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products);

        _logger.LogInformation("Finding similar products for {ProductId} in index {IndexName}",
            request.ProductId, indexName);

        // Step 1: Fetch the source product
        var sourceProduct = await GetSourceProduct(indexName, request.ProductId, cancellationToken);
        if (sourceProduct == null)
        {
            _logger.LogWarning("Source product {ProductId} not found", request.ProductId);
            return new SimilarProductsResponse
            {
                ProductId = request.ProductId,
                Items = [],
                MatchStrategy = "none"
            };
        }

        // Step 2: Build and execute similarity search
        var similarProducts = await FindSimilarProducts(
            indexName,
            sourceProduct,
            request.Limit,
            cancellationToken);

        return new SimilarProductsResponse
        {
            ProductId = request.ProductId,
            Items = similarProducts,
            MatchStrategy = "brand_category_price_tags"
        };
    }

    private async Task<ProductSearchDocument?> GetSourceProduct(
        string indexName,
        string productId,
        CancellationToken cancellationToken)
    {
        var response = await _client.GetAsync<ProductSearchDocument>(
            productId,
            g => g.Index(indexName),
            cancellationToken);

        return response.Found ? response.Source : null;
    }

    private async Task<List<ProductSearchItem>> FindSimilarProducts(
        string indexName,
        ProductSearchDocument sourceProduct,
        int limit,
        CancellationToken cancellationToken)
    {
        // Calculate price range (±30%)
        var minPrice = sourceProduct.Price * 0.7m;
        var maxPrice = sourceProduct.Price * 1.3m;

        var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
            .Index(indexName)
            .Size(limit + 1) // Get one extra to account for filtering source
            .Query(q => BuildSimilarityQuery(q, sourceProduct, minPrice, maxPrice))
            .Sort(sort => sort.Descending(SortSpecialField.Score)),
            cancellationToken);

        if (!searchResponse.IsValid)
        {
            _logger.LogError("Similar products search failed: {Error}", searchResponse.DebugInformation);
            return [];
        }

        // Filter out source product and map results
        return searchResponse.Documents
            .Where(doc => doc.Id != sourceProduct.Id)
            .Take(limit)
            .Select(MapToSearchItem)
            .ToList();
    }

    private QueryContainer BuildSimilarityQuery(
        QueryContainerDescriptor<ProductSearchDocument> q,
        ProductSearchDocument source,
        decimal minPrice,
        decimal maxPrice)
    {
        var should = new List<QueryContainer>();
        var must = new List<QueryContainer>();

        // Must: Active products only
        must.Add(q.Term(t => t.Field(f => f.IsActive).Value(true)));

        // Should: Same category (highest boost)
        if (!string.IsNullOrEmpty(source.CategorySlug))
        {
            should.Add(q.Term(t => t
                .Field(f => f.CategorySlug)
                .Value(source.CategorySlug)
                .Boost(CategoryBoost)));
        }

        // Should: Same brand (high boost)
        if (!string.IsNullOrEmpty(source.BrandSlug))
        {
            should.Add(q.Term(t => t
                .Field(f => f.BrandSlug)
                .Value(source.BrandSlug)
                .Boost(BrandBoost)));
        }

        // Should: Similar price range
        should.Add(q.Range(r => r
            .Field(f => f.Price)
            .GreaterThanOrEquals((double)minPrice)
            .LessThanOrEquals((double)maxPrice)
            .Boost(PriceRangeBoost)));

        // Should: Matching tags
        if (source.Tags != null && source.Tags.Any())
        {
            should.Add(q.Terms(t => t
                .Field(f => f.Tags)
                .Terms(source.Tags)
                .Boost(TagsBoost)));
        }

        // Should: Same department (lower priority)
        if (!string.IsNullOrEmpty(source.DepartmentSlug))
        {
            should.Add(q.Term(t => t
                .Field(f => f.DepartmentSlug)
                .Value(source.DepartmentSlug)
                .Boost(0.5)));
        }

        return q.Bool(b => b
            .Must(must.ToArray())
            .Should(should.ToArray())
            .MinimumShouldMatch(1));
    }

    private ProductSearchItem MapToSearchItem(ProductSearchDocument doc)
    {
        return new ProductSearchItem
        {
            Id = doc.Id,
            Sku = doc.Sku,
            Name = doc.Name,
            Description = doc.Description,
            Brand = doc.Brand,
            BrandSlug = doc.BrandSlug,
            Department = doc.Department,
            DepartmentSlug = doc.DepartmentSlug,
            Category = doc.Category,
            CategorySlug = doc.CategorySlug,
            Price = doc.Price,
            CompareAtPrice = doc.CompareAtPrice,
            DiscountPercentage = doc.DiscountPercentage,
            Currency = doc.Currency,
            ImageUrl = doc.ImageUrls?.FirstOrDefault(),
            ThumbnailUrl = doc.ThumbnailUrl,
            RatingAverage = doc.RatingAverage,
            RatingCount = doc.RatingCount,
            IsDeal = doc.IsDeal,
            IsClearance = doc.IsClearance,
            IsNewArrival = doc.IsNewArrival,
            IsBestSeller = doc.IsBestSeller
        };
    }
}
