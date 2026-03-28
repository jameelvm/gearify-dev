using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IRecommendationsService
{
    Task<RecommendationResponse> GetPersonalizedRecommendationsAsync(string userId, int numResults = 10, CancellationToken cancellationToken = default);
    Task<RecommendationResponse> GetSimilarItemsAsync(string itemId, int numResults = 10, CancellationToken cancellationToken = default);
    Task<RecommendationResponse> GetComplementaryItemsAsync(string itemId, int numResults = 10, CancellationToken cancellationToken = default);
    Task RecordInteractionAsync(string userId, string itemId, string eventType, decimal? eventValue = null, CancellationToken cancellationToken = default);
}
