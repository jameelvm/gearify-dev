using Gearify.SearchService.Application.Mappers;
using Gearify.SearchService.Application.Services;
using Gearify.SearchService.Domain.Events;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.Application.Events;

/// <summary>
/// Processes catalog events and updates the search index accordingly
/// </summary>
public class CatalogEventHandler : ICatalogEventHandler
{
    private readonly IProductIndexService _indexService;
    private readonly ILogger<CatalogEventHandler> _logger;

    public CatalogEventHandler(
        IProductIndexService indexService,
        ILogger<CatalogEventHandler> logger)
    {
        _indexService = indexService;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing catalog event: Type={EventType}, EventId={EventId}, TenantId={TenantId}",
            catalogEvent.EventType,
            catalogEvent.EventId,
            catalogEvent.TenantId);

        try
        {
            return catalogEvent.EventType switch
            {
                CatalogEventTypes.ProductCreated => await HandleProductCreatedAsync(catalogEvent, cancellationToken),
                CatalogEventTypes.ProductUpdated => await HandleProductUpdatedAsync(catalogEvent, cancellationToken),
                CatalogEventTypes.ProductDeleted => await HandleProductDeletedAsync(catalogEvent, cancellationToken),
                _ => HandleUnknownEvent(catalogEvent)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing catalog event: Type={EventType}, EventId={EventId}",
                catalogEvent.EventType,
                catalogEvent.EventId);
            return false;
        }
    }

    private async Task<bool> HandleProductCreatedAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken)
    {
        if (catalogEvent.Payload == null)
        {
            _logger.LogWarning("ProductCreated event has no payload: EventId={EventId}", catalogEvent.EventId);
            return false;
        }

        var document = ProductPayloadMapper.ToSearchDocument(catalogEvent.Payload);

        _logger.LogDebug(
            "Indexing new product: ProductId={ProductId}, Name={Name}",
            document.Id,
            document.Name);

        var success = await _indexService.IndexProductAsync(document, cancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "Successfully indexed new product: ProductId={ProductId}",
                document.Id);
        }

        return success;
    }

    private async Task<bool> HandleProductUpdatedAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken)
    {
        if (catalogEvent.Payload == null)
        {
            _logger.LogWarning("ProductUpdated event has no payload: EventId={EventId}", catalogEvent.EventId);
            return false;
        }

        var document = ProductPayloadMapper.ToSearchDocument(catalogEvent.Payload);

        _logger.LogDebug(
            "Updating product in index: ProductId={ProductId}, Name={Name}",
            document.Id,
            document.Name);

        var success = await _indexService.UpdateProductAsync(document, cancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "Successfully updated product in index: ProductId={ProductId}",
                document.Id);
        }

        return success;
    }

    private async Task<bool> HandleProductDeletedAsync(CatalogEvent catalogEvent, CancellationToken cancellationToken)
    {
        if (catalogEvent.Payload == null)
        {
            _logger.LogWarning("ProductDeleted event has no payload: EventId={EventId}", catalogEvent.EventId);
            return false;
        }

        var productId = catalogEvent.Payload.Id;
        var tenantId = catalogEvent.TenantId;

        _logger.LogDebug(
            "Deleting product from index: ProductId={ProductId}, TenantId={TenantId}",
            productId,
            tenantId);

        var success = await _indexService.DeleteProductAsync(productId, tenantId, cancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "Successfully deleted product from index: ProductId={ProductId}",
                productId);
        }

        return success;
    }

    private bool HandleUnknownEvent(CatalogEvent catalogEvent)
    {
        _logger.LogWarning(
            "Unknown catalog event type: Type={EventType}, EventId={EventId}",
            catalogEvent.EventType,
            catalogEvent.EventId);

        // Return true to acknowledge the message (don't retry unknown events)
        return true;
    }
}
