using Amazon.PersonalizeRuntime.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class RecommendationEnricher : IRecommendationEnricher
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public RecommendationEnricher(IProductRepository productRepository, ITenantContext tenantContext)
    {
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<RecommendationResponse> EnrichPersonalizeResultsAsync(
        List<PredictedItem> items, string source, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var recommendations = new List<ProductRecommendation>();

        foreach (var item in items)
        {
            var product = await _productRepository.GetByIdAsync(item.ItemId, tenantId);
            if (product is null) continue;

            recommendations.Add(new ProductRecommendation
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.Price,
                ThumbnailUrl = product.ThumbnailUrl,
                Category = product.Category,
                Brand = product.Brand,
                Score = item.Score,
                RecommendationReason = "Recommended for you"
            });
        }

        return new RecommendationResponse
        {
            Items = recommendations,
            Source = source,
            TotalCount = recommendations.Count
        };
    }

    public async Task<RecommendationResponse> GetCategoryFallbackAsync(
        string itemId, int numResults, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var product = await _productRepository.GetByIdAsync(itemId, tenantId);
        if (product is null)
            return new RecommendationResponse { Source = RecommendationSources.Empty };

        var products = await _productRepository.GetByCategoryAsync(product.Category, tenantId);

        var recommendations = products
            .Where(p => p.Id != itemId)
            .Take(numResults)
            .Select(p => new ProductRecommendation
            {
                ProductId = p.Id,
                Name = p.Name,
                Price = p.Price,
                ThumbnailUrl = p.ThumbnailUrl,
                Category = p.Category,
                Brand = p.Brand,
                Score = 0.5,
                RecommendationReason = $"More in {product.Category}"
            })
            .ToList();

        return new RecommendationResponse
        {
            Items = recommendations,
            Source = RecommendationSources.CategoryFallback,
            TotalCount = recommendations.Count
        };
    }

    public async Task<RecommendationResponse> GetPopularProductsFallbackAsync(
        int numResults, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var products = await _productRepository.GetAllAsync(tenantId, take: numResults);

        var recommendations = products
            .Where(p => p.IsBestSeller || p.IsFeatured)
            .DefaultIfEmpty()
            .Where(p => p is not null)
            .Take(numResults)
            .Select(p => new ProductRecommendation
            {
                ProductId = p!.Id,
                Name = p.Name,
                Price = p.Price,
                ThumbnailUrl = p.ThumbnailUrl,
                Category = p.Category,
                Brand = p.Brand,
                Score = 0.3,
                RecommendationReason = "Popular product"
            })
            .ToList();

        if (recommendations.Count == 0)
        {
            recommendations = products.Take(numResults).Select(p => new ProductRecommendation
            {
                ProductId = p.Id,
                Name = p.Name,
                Price = p.Price,
                ThumbnailUrl = p.ThumbnailUrl,
                Category = p.Category,
                Brand = p.Brand,
                Score = 0.1,
                RecommendationReason = "You might like"
            }).ToList();
        }

        return new RecommendationResponse
        {
            Items = recommendations,
            Source = RecommendationSources.PopularFallback,
            TotalCount = recommendations.Count
        };
    }
}
