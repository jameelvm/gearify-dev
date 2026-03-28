namespace Gearify.CatalogService.Application.Services.Recommendations;

public interface IInteractionRecorderService
{
    Task RecordInteractionAsync(string userId, string itemId, string eventType, decimal? eventValue = null, CancellationToken cancellationToken = default);
}
