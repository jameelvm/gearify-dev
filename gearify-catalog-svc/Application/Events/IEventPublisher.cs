using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Application.Events;

/// <summary>
/// Publishes domain events to SNS for other services to consume
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish event when a new product is created
    /// </summary>
    Task PublishProductCreatedAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish event when a product is updated
    /// </summary>
    Task PublishProductUpdatedAsync(Product product, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish event when a product is deleted
    /// </summary>
    Task PublishProductDeletedAsync(string productId, string tenantId, CancellationToken cancellationToken = default);
}
