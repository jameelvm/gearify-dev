using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IPersonalizedRecommendationService
{
    Task<RecommendationResponse> GetPersonalizedRecommendationsAsync(string userId, int numResults = 10, CancellationToken cancellationToken = default);
}
