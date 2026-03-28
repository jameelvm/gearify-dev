using Amazon.PersonalizeRuntime.Model;
using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IRecommendationEnricher
{
    Task<RecommendationResponse> EnrichPersonalizeResultsAsync(List<PredictedItem> items, string source, CancellationToken ct);
    Task<RecommendationResponse> GetCategoryFallbackAsync(string itemId, int numResults, CancellationToken ct);
    Task<RecommendationResponse> GetPopularProductsFallbackAsync(int numResults, CancellationToken ct);
}
