using Gearify.MediaService.Application.BackgroundJobs.Models;
using Gearify.MediaService.Application.Services;
using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Infrastructure.Constants;
using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Application.BackgroundJobs;

/// <summary>
/// Background service that processes images asynchronously
/// Polls SQS queue for image processing messages
/// NOTE: This can be extracted into a separate microservice later by:
/// 1. Moving this folder to new project
/// 2. Replacing IImageProcessingQueue with HTTP client to call Media Service API
/// </summary>
public class ImageProcessingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ImageProcessingBackgroundService> _logger;

    public ImageProcessingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ImageProcessingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Image Processing Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Image Processing Background Service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Image Processing Background Service stopped");
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var queue = scope.ServiceProvider.GetRequiredService<IImageProcessingQueue>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var imageProcessor = scope.ServiceProvider.GetRequiredService<IImageProcessor>();
        var mediaRepository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();

        // Receive messages from queue (long polling)
        var messages = await queue.ReceiveMessagesAsync(
            maxMessages: 10,
            waitTimeSeconds: 20,
            cancellationToken: cancellationToken);

        if (!messages.Any())
        {
            return; // No messages, loop will retry
        }

        _logger.LogInformation("Received {Count} messages from queue", messages.Count);

        // Process messages in parallel
        var tasks = messages.Select(msg => ProcessSingleMessageAsync(
            msg,
            queue,
            storageService,
            imageProcessor,
            mediaRepository,
            cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task ProcessSingleMessageAsync(
        QueueMessage<ImageProcessingMessage> queueMessage,
        IImageProcessingQueue queue,
        IStorageService storageService,
        IImageProcessor imageProcessor,
        IMediaRepository mediaRepository,
        CancellationToken cancellationToken)
    {
        var message = queueMessage.Body;

        try
        {
            _logger.LogInformation(
                "Processing image {MediaId} for {EntityType} {EntityId}",
                message.MediaId,
                message.EntityType,
                message.EntityId);

            // Download original from S3
            var originalStream = await storageService.GetAsync(message.OriginalKey, cancellationToken);

            // Generate variants
            Dictionary<ImageSize, Stream> variants;
            try
            {
                variants = await imageProcessor.GenerateVariantsAsync(originalStream, message.ContentType);
            }
            finally
            {
                await originalStream.DisposeAsync();
            }

            // Prepare S3 keys for variant images (skip original as it's already uploaded)
            var keys = new Dictionary<ImageSize, string>();
            foreach (var size in new[] { ImageSize.Thumbnail, ImageSize.Medium, ImageSize.Large })
            {
                var key = string.Format(
                    StorageConstants.S3PathTemplate,
                    message.TenantId,
                    message.EntityType.ToLower(),
                    message.EntityId,
                    size.ToString().ToLower(),
                    Path.GetFileName(message.OriginalKey));

                keys[size] = key;
            }

            // Upload variant images to S3
            try
            {
                foreach (var variant in variants.Where(v => v.Key != ImageSize.Original))
                {
                    var stream = variant.Value;
                    var key = keys[variant.Key];

                    stream.Position = 0;
                    await storageService.UploadAsync(stream, key, message.ContentType, cancellationToken);

                    _logger.LogDebug("Uploaded {Size} variant for {MediaId}", variant.Key, message.MediaId);
                }
            }
            finally
            {
                // Cleanup streams
                foreach (var stream in variants.Values)
                {
                    await stream.DisposeAsync();
                }
            }

            // Update metadata in DynamoDB
            var media = await mediaRepository.GetByIdAsync(message.MediaId, message.TenantId);
            if (media != null)
            {
                media.ThumbnailKey = keys[ImageSize.Thumbnail];
                media.MediumKey = keys[ImageSize.Medium];
                media.LargeKey = keys[ImageSize.Large];
                media.Status = ProcessingStatus.Ready.ToString();
                media.ProcessedAt = DateTime.UtcNow;

                await mediaRepository.UpdateAsync(media);

                _logger.LogInformation(
                    "Successfully processed image {MediaId}. All variants ready.",
                    message.MediaId);
            }
            else
            {
                _logger.LogWarning("Media metadata not found for {MediaId}", message.MediaId);
            }

            // Delete message from queue (success)
            await queue.DeleteMessageAsync(queueMessage.ReceiptHandle, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing image {MediaId}. Message will be retried.",
                message.MediaId);

            // Update metadata with error status
            try
            {
                var media = await mediaRepository.GetByIdAsync(message.MediaId, message.TenantId);
                if (media != null)
                {
                    media.Status = ProcessingStatus.Failed.ToString();
                    media.ProcessingError = ex.Message;
                    media.ProcessedAt = DateTime.UtcNow;
                    await mediaRepository.UpdateAsync(media);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating failed status for {MediaId}", message.MediaId);
            }

            // Return message to queue for retry (or let it go to DLQ after max retries)
            await queue.ReturnMessageAsync(queueMessage.ReceiptHandle, visibilityTimeoutSeconds: 60, cancellationToken);
        }
    }
}
