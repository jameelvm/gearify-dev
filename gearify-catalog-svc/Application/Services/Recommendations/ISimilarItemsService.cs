using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface ISimilarItemsService
{
    Task<RecommendationResponse> GetSimilarItemsAsync(string itemId, int numResults = 10, CancellationToken cancellationToken = default);
}
