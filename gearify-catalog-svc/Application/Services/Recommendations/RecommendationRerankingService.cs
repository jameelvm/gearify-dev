using Amazon.PersonalizeRuntime;
using Amazon.PersonalizeRuntime.Model;
using Gearify.SharedKernel.AI;
using Gearify.SharedKernel.AI.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class RecommendationRerankingService : IRecommendationRerankingService
{
    private readonly IAmazonPersonalizeRuntime _personalizeRuntime;
    private readonly AICircuitBreakerPolicy _circuitBreaker;
    private readonly ILogger<RecommendationRerankingService> _logger;
    private readonly AIServiceConfiguration _config;

    public RecommendationRerankingService(
        IAmazonPersonalizeRuntime personalizeRuntime,
        AICircuitBreakerPolicy circuitBreaker,
        ILogger<RecommendationRerankingService> logger,
        IOptions<AIServiceConfiguration> config)
    {
        _personalizeRuntime = personalizeRuntime;
        _circuitBreaker = circuitBreaker;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<List<string>> RerankPersonalizedAsync(
        string userId, List<string> itemIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.PersonalizeCampaignArn) || itemIds.Count == 0)
            return itemIds;

        try
        {
            var pipeline = _circuitBreaker.GetPipeline("personalize-rerank");
            return await pipeline.ExecuteAsync(async ct =>
            {
                var response = await _personalizeRuntime.GetPersonalizedRankingAsync(
                    new GetPersonalizedRankingRequest
                    {
                        CampaignArn = _config.PersonalizeCampaignArn,
                        UserId = userId,
                        InputList = itemIds
                    }, ct);

                return response.PersonalizedRanking.Select(r => r.ItemId).ToList();
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Personalize reranking failed for user {UserId}", userId);
            return itemIds;
        }
    }
}
