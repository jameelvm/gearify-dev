using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CatalogService.Infrastructure.Messaging.Events.Inbound;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// Handles image processing completed events from Media Service.
/// Updates Product.ThumbnailUrl when image processing is complete.
/// </summary>
public class ImageProcessingCompletedEventHandler : IEventHandler<ImageProcessingCompletedEventMessage>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ImageProcessingCompletedEventHandler> _logger;

    public ImageProcessingCompletedEventHandler(
        IProductRepository productRepository,
        ILogger<ImageProcessingCompletedEventHandler> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ImageProcessingCompletedEventMessage evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing thumbnail update for {EntityType} {EntityId}",
            evt.EntityType,
            evt.EntityId);

        // Only process Product entities
        if (evt.EntityType != "Product")
        {
            _logger.LogDebug(
                "Skipping non-Product entity: {EntityType} {EntityId}",
                evt.EntityType,
                evt.EntityId);
            return true;
        }

        // Get the product
        var product = await _productRepository.GetByIdAsync(evt.EntityId, evt.TenantId);

        if (product == null)
        {
            _logger.LogWarning(
                "Product not found: {ProductId} (Tenant: {TenantId})",
                evt.EntityId,
                evt.TenantId);
            return true;
        }

        // Update the product's thumbnail URL
        // Only update if it's the first image (displayOrder = 0) or if ThumbnailUrl is currently null
        if (evt.DisplayOrder == 0 || string.IsNullOrEmpty(product.ThumbnailUrl))
        {
            product.ThumbnailUrl = evt.ThumbnailUrl;
            product.UpdatedAt = DateTime.UtcNow;

            await _productRepository.UpdateAsync(product);

            _logger.LogInformation(
                "Updated Product {ProductId} with thumbnail URL: {ThumbnailUrl}",
                evt.EntityId,
                evt.ThumbnailUrl);
        }
        else
        {
            _logger.LogDebug(
                "Skipping thumbnail update for Product {ProductId} - not first image (DisplayOrder: {DisplayOrder})",
                evt.EntityId,
                evt.DisplayOrder);
        }

        return true;
    }
}
