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

public class PersonalizedRecommendationService : IPersonalizedRecommendationService
{
    private readonly IAmazonPersonalizeRuntime _personalizeRuntime;
    private readonly IAICacheService _cache;
    private readonly AICircuitBreakerPolicy _circuitBreaker;
    private readonly IRecommendationEnricher _enricher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PersonalizedRecommendationService> _logger;
    private readonly AIServiceConfiguration _config;

    public PersonalizedRecommendationService(
        IAmazonPersonalizeRuntime personalizeRuntime,
        IAICacheService cache,
        AICircuitBreakerPolicy circuitBreaker,
        IRecommendationEnricher enricher,
        ITenantContext tenantContext,
        ILogger<PersonalizedRecommendationService> logger,
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

    public async Task<RecommendationResponse> GetPersonalizedRecommendationsAsync(
        string userId, int numResults = 10, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"reco:personal:{_tenantContext.TenantId}:{userId}:{numResults}";
        var cached = await _cache.GetAsync<RecommendationResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached with { Source = RecommendationSources.Cache };

        if (!string.IsNullOrEmpty(_config.PersonalizeCampaignArn))
        {
            try
            {
                var pipeline = _circuitBreaker.GetPipeline("personalize");
                var result = await pipeline.ExecuteAsync(async ct =>
                {
                    var response = await _personalizeRuntime.GetRecommendationsAsync(new GetRecommendationsRequest
                    {
                        CampaignArn = _config.PersonalizeCampaignArn,
                        UserId = userId,
                        NumResults = numResults
                    }, ct);

                    return await _enricher.EnrichPersonalizeResultsAsync(response.ItemList, RecommendationSources.Personalize, ct);
                }, cancellationToken);

                await _cache.SetAsync(cacheKey, result, _config.Cache.UserRecommendations, cancellationToken);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Personalize failed for user {UserId}, falling back", userId);
            }
        }

        return await _enricher.GetPopularProductsFallbackAsync(numResults, cancellationToken);
    }
}
