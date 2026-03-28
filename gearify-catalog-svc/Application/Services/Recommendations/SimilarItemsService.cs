using Amazon.PersonalizeRuntime;
using Amazon.PersonalizeRuntime.Model;
using Gearify.CatalogService.Application.DTOs;
using Gearify.SharedKernel.AI;
using Gearify.SharedKernel.AI.Caching;
using Gearify.SharedKernel.AI.Resilience;
using Gearify.SharedKernel.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class SimilarItemsService : ISimilarItemsService
{
    private readonly IAmazonPersonalizeRuntime _personalizeRuntime;
    private readonly IAICacheService _cache;
    private readonly AICircuitBreakerPolicy _circuitBreaker;
    private readonly IRecommendationEnricher _enricher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SimilarItemsService> _logger;
    private readonly AIServiceConfiguration _config;

    public SimilarItemsService(
        IAmazonPersonalizeRuntime personalizeRuntime,
        IAICacheService cache,
        AICircuitBreakerPolicy circuitBreaker,
        IRecommendationEnricher enricher,
        ITenantContext tenantContext,
        ILogger<SimilarItemsService> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _personalizeRuntime = personalizeRuntime;
        _cache = cache;
        _circuitBreaker = circuitBreaker;
        _enricher = enricher;
        _tenantContext = tenantContext;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<RecommendationResponse> GetSimilarItemsAsync(
        string itemId, int numResults = 10, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"reco:similar:{_tenantContext.TenantId}:{itemId}:{numResults}";
        var cached = await _cache.GetAsync<RecommendationResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached with { Source = RecommendationSources.Cache };

        if (!string.IsNullOrEmpty(_config.PersonalizeSimilarItemsCampaignArn))
        {
            try
            {
                var pipeline = _circuitBreaker.GetPipeline("personalize-similar");
                var result = await pipeline.ExecuteAsync(async ct =>
                {
                    var response = await _personalizeRuntime.GetRecommendationsAsync(new GetRecommendationsRequest
                    {
                        CampaignArn = _config.PersonalizeSimilarItemsCampaignArn,
                        ItemId = itemId,
                        NumResults = numResults
                    }, ct);

                    return await _enricher.EnrichPersonalizeResultsAsync(response.ItemList, RecommendationSources.Personalize, ct);
                }, cancellationToken);

                await _cache.SetAsync(cacheKey, result, _config.Cache.SimilarItems, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Personalize similar items failed for {ItemId}, falling back", itemId);
            }
        }

        return await _enricher.GetCategoryFallbackAsync(itemId, numResults, cancellationToken);
    }
}
