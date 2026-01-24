using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.MediaService.Application.Services;
using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Domain.Events;
using Gearify.MediaService.Infrastructure.Constants;
using Gearify.MediaService.Infrastructure.Messaging.Events.Inbound;
using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace Gearify.MediaService.Infrastructure.Messaging;

/// <summary>
/// Handles image processing events.
/// Downloads original image, generates variants, uploads to S3,
/// updates metadata, and publishes completion event.
/// </summary>
public class ImageProcessingEventHandler : IEventHandler<ImageProcessingEventMessage>
{
    private readonly IStorageService _storageService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IMediaRepository _mediaRepository;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly IMediaUrlFactory _urlFactory;
    private readonly ILogger<ImageProcessingEventHandler> _logger;

    public ImageProcessingEventHandler(
        IStorageService storageService,
        IImageProcessor imageProcessor,
        IMediaRepository mediaRepository,
        ISnsEventPublisher eventPublisher,
        IMediaUrlFactory urlFactory,
        ILogger<ImageProcessingEventHandler> logger)
    {
        _storageService = storageService;
        _imageProcessor = imageProcessor;
        _mediaRepository = mediaRepository;
        _eventPublisher = eventPublisher;
        _urlFactory = urlFactory;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(ImageProcessingEventMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing image {MediaId} for {EntityType} {EntityId}",
            message.MediaId,
            message.EntityType,
            message.EntityId);

        try
        {
            // Download original from S3
            var originalStream = await _storageService.GetAsync(message.OriginalKey, cancellationToken);

            // Generate variants
            Dictionary<ImageSize, Stream> variants;
            try
            {
                variants = await _imageProcessor.GenerateVariantsAsync(originalStream, message.ContentType);
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
                    await _storageService.UploadAsync(stream, key, message.ContentType, cancellationToken);
                    _logger.LogInformation("Successfully uploaded {Size} variant for {MediaId}", variant.Key, message.MediaId);
                }
            }
            finally
            {
                foreach (var stream in variants.Values)
                {
                    await stream.DisposeAsync();
                }
            }

            // Update metadata in DynamoDB
            var media = await _mediaRepository.GetByIdAsync(message.MediaId, message.TenantId);
            if (media != null)
            {
                media.ThumbnailKey = keys[ImageSize.Thumbnail];
                media.MediumKey = keys[ImageSize.Medium];
                media.LargeKey = keys[ImageSize.Large];
                media.Status = ProcessingStatus.Ready.ToString();
                media.ProcessedAt = DateTime.UtcNow;

                await _mediaRepository.UpdateAsync(media);

                _logger.LogInformation(
                    "Successfully processed image {MediaId}. All variants ready.",
                    message.MediaId);

                // Publish event to notify other services
                var completedEvent = new ImageProcessingCompletedEvent(
                    MediaId: message.MediaId,
                    EntityType: message.EntityType,
                    EntityId: message.EntityId,
                    TenantId: message.TenantId,
                    ThumbnailUrl: _urlFactory.GetUrl(keys[ImageSize.Thumbnail]),
                    MediumUrl: _urlFactory.GetUrl(keys[ImageSize.Medium]),
                    LargeUrl: _urlFactory.GetUrl(keys[ImageSize.Large]),
                    OriginalUrl: _urlFactory.GetUrl(message.OriginalKey),
                    DisplayOrder: media.DisplayOrder,
                    AltText: media.AltText,
                    OccurredAt: DateTime.UtcNow
                );

                await _eventPublisher.PublishAsync(completedEvent, cancellationToken);

                _logger.LogInformation(
                    "Published ImageProcessingCompletedEvent for {EntityType} {EntityId}",
                    message.EntityType,
                    message.EntityId);
            }
            else
            {
                _logger.LogWarning("Media metadata not found for {MediaId}", message.MediaId);
            }

            return true;
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
                var media = await _mediaRepository.GetByIdAsync(message.MediaId, message.TenantId);
                if (media != null)
                {
                    media.Status = ProcessingStatus.Failed.ToString();
                    media.ProcessingError = ex.Message;
                    media.ProcessedAt = DateTime.UtcNow;
                    await _mediaRepository.UpdateAsync(media);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating failed status for {MediaId}", message.MediaId);
            }

            throw; // Rethrow so the processor leaves the message for retry
        }
    }
}
