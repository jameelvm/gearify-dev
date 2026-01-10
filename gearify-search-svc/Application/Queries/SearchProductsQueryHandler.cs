using Gearify.SearchService.Application.DTOs;
using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Gearify.SearchService.Infrastructure.OpenSearch;
using MediatR;
using Microsoft.Extensions.Logging;
using Nest;

namespace Gearify.SearchService.Application.Queries;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResponse>
{
    private readonly IElasticClient _client;
    private readonly IIndexManager _indexManager;
    private readonly ILogger<SearchProductsQueryHandler> _logger;

    public SearchProductsQueryHandler(
        IOpenSearchClientFactory clientFactory,
        IIndexManager indexManager,
        ILogger<SearchProductsQueryHandler> logger)
    {
        _client = clientFactory.CreateClient();
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task<SearchProductsResponse> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? "default-tenant";
        var indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products);

        _logger.LogDebug("Searching products in index {IndexName} with query: {Query}", indexName, request.SearchTerm);

        // Build and execute search
        var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
            .Index(indexName)
            .From((request.Page - 1) * request.PageSize)
            .Size(request.PageSize)
            .Query(q => BuildQuery(q, request))
            .Sort(sort => BuildSort(sort, request.SortBy, request.SortDirection))
            .Aggregations(BuildAggregations),
            cancellationToken
        );

        if (!searchResponse.IsValid)
        {
            _logger.LogError("Search failed: {Error}", searchResponse.DebugInformation);
            return new SearchProductsResponse
            {
                Items = [],
                TotalCount = 0,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }

        return MapToResponse(searchResponse, request);
    }

    private QueryContainer BuildQuery(QueryContainerDescriptor<ProductSearchDocument> q, SearchProductsQuery request)
    {
        var must = new List<QueryContainer>();

        // Active products only
        must.Add(q.Term(t => t.Field(f => f.IsActive).Value(true)));

        // Full-text search
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            must.Add(q.MultiMatch(mm => mm
                .Fields(f => f
                    .Field(p => p.Name, boost: 3)
                    .Field("name.autocomplete", boost: 2)
                    .Field(p => p.Description)
                    .Field(p => p.Brand, boost: 2)
                    .Field(p => p.Category)
                    .Field(p => p.Tags)
                )
                .Query(request.SearchTerm)
                .Type(TextQueryType.BestFields)
                .Fuzziness(Fuzziness.Auto)
            ));
        }

        // Category filter
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            must.Add(q.Term(t => t.Field(f => f.CategorySlug).Value(request.Category.ToLowerInvariant())));
        }

        // Brand filter
        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            must.Add(q.Term(t => t.Field(f => f.BrandSlug).Value(request.Brand.ToLowerInvariant())));
        }

        // Department filter
        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            must.Add(q.Term(t => t.Field(f => f.DepartmentSlug).Value(request.Department.ToLowerInvariant())));
        }

        // Price range filter
        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
        {
            must.Add(q.Range(r =>
            {
                var range = r.Field(f => f.Price);
                if (request.MinPrice.HasValue)
                    range = range.GreaterThanOrEquals((double)request.MinPrice.Value);
                if (request.MaxPrice.HasValue)
                    range = range.LessThanOrEquals((double)request.MaxPrice.Value);
                return range;
            }));
        }

        // Rating filter
        if (request.MinRating.HasValue)
        {
            must.Add(q.Range(r => r
                .Field(f => f.RatingAverage)
                .GreaterThanOrEquals((double)request.MinRating.Value)
            ));
        }

        // Tags filter
        if (request.Tags != null && request.Tags.Any())
        {
            must.Add(q.Terms(t => t.Field(f => f.Tags).Terms(request.Tags)));
        }

        // Deals filter
        if (request.DealsOnly == true)
        {
            must.Add(q.Term(t => t.Field(f => f.IsDeal).Value(true)));
        }

        // Clearance filter
        if (request.ClearanceOnly == true)
        {
            must.Add(q.Term(t => t.Field(f => f.IsClearance).Value(true)));
        }

        // New arrivals filter
        if (request.NewArrivalsOnly == true)
        {
            must.Add(q.Term(t => t.Field(f => f.IsNewArrival).Value(true)));
        }

        // Best sellers filter
        if (request.BestSellersOnly == true)
        {
            must.Add(q.Term(t => t.Field(f => f.IsBestSeller).Value(true)));
        }

        return q.Bool(b => b.Must(must.ToArray()));
    }

    private SortDescriptor<ProductSearchDocument> BuildSort(
        SortDescriptor<ProductSearchDocument> sort,
        string? sortBy,
        string? sortDirection)
    {
        var order = sortDirection?.ToLowerInvariant() == "asc" ? SortOrder.Ascending : SortOrder.Descending;

        return sortBy?.ToLowerInvariant() switch
        {
            "price" => sort.Field(f => f
                .Field(p => p.Price)
                .Order(order)
                .Missing(order == SortOrder.Ascending ? "_last" : "_first")),
            "name" => sort.Field(f => f
                .Field("name.keyword")
                .Order(order)),
            "rating" => sort.Field(f => f
                .Field(p => p.RatingAverage)
                .Order(order)
                .Missing("_last")),
            "newest" => sort.Field(f => f
                .Field(p => p.CreatedAt)
                .Order(order)),
            "popularity" => sort.Field(f => f
                .Field(p => p.RatingCount)
                .Order(order)
                .Missing("_last")),
            _ => sort.Field("_score", SortOrder.Descending) // relevance (default)
        };
    }

    private AggregationContainerDescriptor<ProductSearchDocument> BuildAggregations(
        AggregationContainerDescriptor<ProductSearchDocument> a)
    {
        return a
            .Terms("brands", t => t
                .Field("brand.keyword")
                .Size(50)
            )
            .Terms("categories", t => t
                .Field(f => f.Category)
                .Size(50)
            )
            .Terms("departments", t => t
                .Field(f => f.Department)
                .Size(20)
            )
            .Range("price_ranges", r => r
                .Field(f => f.Price)
                .Ranges(
                    range => range.Key("under_25").To(25),
                    range => range.Key("25_to_50").From(25).To(50),
                    range => range.Key("50_to_100").From(50).To(100),
                    range => range.Key("100_to_200").From(100).To(200),
                    range => range.Key("over_200").From(200)
                )
            )
            .Range("ratings", r => r
                .Field(f => f.RatingAverage)
                .Ranges(
                    range => range.Key("4_and_up").From(4),
                    range => range.Key("3_and_up").From(3),
                    range => range.Key("2_and_up").From(2),
                    range => range.Key("1_and_up").From(1)
                )
            );
    }

    private SearchProductsResponse MapToResponse(ISearchResponse<ProductSearchDocument> response, SearchProductsQuery request)
    {
        var items = response.Documents.Select(doc => new ProductSearchItem
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
        }).ToList();

        return new SearchProductsResponse
        {
            Items = items,
            TotalCount = response.Total,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)response.Total / request.PageSize),
            Facets = MapFacets(response.Aggregations)
        };
    }

    private SearchFacets MapFacets(AggregateDictionary aggregations)
    {
        var facets = new SearchFacets();

        // Brands
        var brandsBucket = aggregations.Terms("brands");
        if (brandsBucket != null)
        {
            facets.Brands = brandsBucket.Buckets
                .Select(b => new FacetItem { Key = b.Key, Count = b.DocCount ?? 0 })
                .ToList();
        }

        // Categories
        var categoriesBucket = aggregations.Terms("categories");
        if (categoriesBucket != null)
        {
            facets.Categories = categoriesBucket.Buckets
                .Select(b => new FacetItem { Key = b.Key, Count = b.DocCount ?? 0 })
                .ToList();
        }

        // Departments
        var departmentsBucket = aggregations.Terms("departments");
        if (departmentsBucket != null)
        {
            facets.Departments = departmentsBucket.Buckets
                .Select(b => new FacetItem { Key = b.Key, Count = b.DocCount ?? 0 })
                .ToList();
        }

        // Price ranges
        var priceRanges = aggregations.Range("price_ranges");
        if (priceRanges != null)
        {
            facets.PriceRanges = priceRanges.Buckets
                .Select(b => new FacetItem { Key = b.Key, Count = b.DocCount })
                .ToList();
        }

        // Ratings
        var ratings = aggregations.Range("ratings");
        if (ratings != null)
        {
            facets.Ratings = ratings.Buckets
                .Select(b => new FacetItem { Key = b.Key, Count = b.DocCount })
                .ToList();
        }

        return facets;
    }
}
