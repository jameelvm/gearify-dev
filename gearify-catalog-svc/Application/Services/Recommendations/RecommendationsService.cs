using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public class RecommendationsService : IRecommendationsService
{
    private readonly IPersonalizedRecommendationService _personalizedService;
    private readonly ISimilarItemsService _similarItemsService;
    private readonly IComplementaryItemsService _complementaryItemsService;
    private readonly IInteractionRecorderService _interactionRecorderService;

    public RecommendationsService(
        IPersonalizedRecommendationService personalizedService,
        ISimilarItemsService similarItemsService,
        IComplementaryItemsService complementaryItemsService,
        IInteractionRecorderService interactionRecorderService)
    {
        _personalizedService = personalizedService;
        _similarItemsService = similarItemsService;
        _complementaryItemsService = complementaryItemsService;
        _interactionRecorderService = interactionRecorderService;
    }

    public Task<RecommendationResponse> GetPersonalizedRecommendationsAsync(
        string userId, int numResults = 10, CancellationToken cancellationToken = default)
        => _personalizedService.GetPersonalizedRecommendationsAsync(userId, numResults, cancellationToken);

    public Task<RecommendationResponse> GetSimilarItemsAsync(
        string itemId, int numResults = 10, CancellationToken cancellationToken = default)
        => _similarItemsService.GetSimilarItemsAsync(itemId, numResults, cancellationToken);

    public Task<RecommendationResponse> GetComplementaryItemsAsync(
        string itemId, int numResults = 10, CancellationToken cancellationToken = default)
        => _complementaryItemsService.GetComplementaryItemsAsync(itemId, numResults, cancellationToken);

    public Task RecordInteractionAsync(
        string userId, string itemId, string eventType, decimal? eventValue = null,
        CancellationToken cancellationToken = default)
        => _interactionRecorderService.RecordInteractionAsync(userId, itemId, eventType, eventValue, cancellationToken);
}
