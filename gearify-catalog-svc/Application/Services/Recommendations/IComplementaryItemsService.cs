using Gearify.CatalogService.Application.DTOs;

namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IComplementaryItemsService
{
    Task<RecommendationResponse> GetComplementaryItemsAsync(string itemId, int numResults = 10, CancellationToken cancellationToken = default);
}
