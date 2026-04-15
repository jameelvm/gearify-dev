using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.AI;
using Gearify.SharedKernel.AI.Caching;
using Gearify.SharedKernel.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class ComplementaryItemsService : IComplementaryItemsService
{
    private readonly IProductRepository _productRepository;
    private readonly IAICacheService _cache;
    private readonly IRecommendationEnricher _enricher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ComplementaryItemsService> _logger;
    private readonly AIServiceConfiguration _config;

    private static readonly Dictionary<string, List<string>> ComplementaryCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        // Match actual DynamoDB category names
        ["Bats"] = ["Pads", "Gloves", "Helmets", "Bags", "Accessories"],
        ["Pads"] = ["Bats", "Gloves", "Helmets", "Accessories"],
        ["Gloves"] = ["Bats", "Pads", "Helmets"],
        ["Helmets"] = ["Bats", "Pads", "Gloves"],
        ["Balls"] = ["Bats", "Gloves", "Bags"],
        ["Bags"] = ["Bats", "Accessories", "Balls"],
        ["Accessories"] = ["Bats", "Bags", "Balls"],
    };

    public ComplementaryItemsService(
        IProductRepository productRepository,
        IAICacheService cache,
        IRecommendationEnricher enricher,
        ITenantContext tenantContext,
        ILogger<ComplementaryItemsService> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _productRepository = productRepository;
        _cache = cache;
        _enricher = enricher;
        _tenantContext = tenantContext;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<RecommendationResponse> GetComplementaryItemsAsync(
        string itemId, int numResults = 10, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        var cacheKey = $"reco:complementary:{tenantId}:{itemId}:{numResults}";
        var cached = await _cache.GetAsync<RecommendationResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached with { Source = RecommendationSources.Cache };

        var product = await _productRepository.GetByIdAsync(itemId, tenantId);
        if (product is null)
            return new RecommendationResponse { Source = RecommendationSources.Empty };

        if (ComplementaryCategories.TryGetValue(product.Category, out var complementaryCategories))
        {
            var recommendations = new List<ProductRecommendation>();

            foreach (var category in complementaryCategories.Take(3))
            {
                var products = await _productRepository.GetByCategoryAsync(category, tenantId);

                foreach (var p in products.Take(numResults / 3 + 1))
                {
                    recommendations.Add(new ProductRecommendation
                    {
                        ProductId = p.Id,
                        Name = p.Name,
                        Price = p.Price,
                        ThumbnailUrl = p.ThumbnailUrl,
                        Category = p.Category,
                        Brand = p.Brand,
                        Score = 0.8,
                        RecommendationReason = $"Frequently bought with {product.Category}"
                    });
                }
            }

            var result = new RecommendationResponse
            {
                Items = recommendations.Take(numResults).ToList(),
                Source = RecommendationSources.ComplementaryRules,
                TotalCount = recommendations.Count
            };

            await _cache.SetAsync(cacheKey, result, _config.Cache.SimilarItems, cancellationToken);
            return result;
        }

        return await _enricher.GetCategoryFallbackAsync(itemId, numResults, cancellationToken);
    }
}
