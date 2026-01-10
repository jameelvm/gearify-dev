using Gearify.SearchService.Domain.Events;

namespace Gearify.SearchService.Application.Events;

/// <summary>
/// Handles catalog events received from SQS
/// </summary>
public interface ICatalogEventHandler
{
    /// <summary>
    /// Process a catalog event (create, update, or delete product in search index)
    /// </summary>
    Task<bool> HandleAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken = default);
}
