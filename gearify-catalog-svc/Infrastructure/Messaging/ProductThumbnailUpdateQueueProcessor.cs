using Gearify.CatalogService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using Gearify.CatalogService.Infrastructure.Repositories;

namespace Gearify.CatalogService.Infrastructure.Messaging;

/// <summary>
/// Background service that listens for ImageProcessingCompleted events from Media Service
/// and updates Product.ThumbnailUrl when image processing is complete
/// </summary>
public class ProductThumbnailUpdateQueueProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductThumbnailUpdateQueueProcessor> _logger;

    public ProductThumbnailUpdateQueueProcessor(
        IServiceProvider serviceProvider,
        ILogger<ProductThumbnailUpdateQueueProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Product Thumbnail Update Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Product Thumbnail Update Background Service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Product Thumbnail Update Background Service stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IEventQueue<ImageProcessingCompletedEventMessage>>();
        var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        // Receive messages from queue (long polling)
        var messages = await queue.ReceiveMessagesAsync(
            maxMessages: 10,
            waitTimeSeconds: 20,
            cancellationToken: cancellationToken);

        if (!messages.Any())
        {
            return; // No messages, loop will retry
        }

        _logger.LogInformation("Received {Count} image processing completed events", messages.Count);

        // Process messages sequentially (could be parallel if needed)
        foreach (var message in messages)
        {
            await ProcessSingleMessageAsync(message, queue, productRepository, cancellationToken);
        }
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<ImageProcessingCompletedEventMessage> queueMessage,
        IEventQueue<ImageProcessingCompletedEventMessage> queue,
        IProductRepository productRepository,
        CancellationToken cancellationToken)
    {
        var evt = queueMessage.Body;

        try
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
                await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
                return;
            }

            // Get the product
            var product = await productRepository.GetByIdAsync(evt.EntityId, evt.TenantId);

            if (product == null)
            {
                _logger.LogWarning(
                    "Product not found: {ProductId} (Tenant: {TenantId})",
                    evt.EntityId,
                    evt.TenantId);
                await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
                return;
            }

            // Update the product's thumbnail URL
            // Only update if it's the first image (displayOrder = 0) or if ThumbnailUrl is currently null
            if (evt.DisplayOrder == 0 || string.IsNullOrEmpty(product.ThumbnailUrl))
            {
                product.ThumbnailUrl = evt.ThumbnailUrl;
                product.UpdatedAt = DateTime.UtcNow;

                await productRepository.UpdateAsync(product);

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

            // Delete message from queue (success)
            await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error updating thumbnail for {EntityType} {EntityId}. Message will be retried.",
                evt.EntityType,
                evt.EntityId);

            // Don't delete the message - it will be retried
            // SQS will eventually move it to DLQ if it keeps failing
        }
    }
}
