namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IRecommendationRerankingService
{
    Task<List<string>> RerankPersonalizedAsync(string userId, List<string> itemIds, CancellationToken cancellationToken = default);
}
