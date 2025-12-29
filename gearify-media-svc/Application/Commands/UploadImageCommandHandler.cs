using Gearify.MediaService.Application.Events;
using Gearify.MediaService.Application.Services;
using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Infrastructure.Configuration;
using Gearify.MediaService.Infrastructure.Constants;
using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.MediaService.Application.Commands;

/// <summary>
/// Handler for uploading images
/// </summary>
public class UploadImageCommandHandler : IRequestHandler<UploadImageCommand, UploadImageResult>
{
    private readonly IStorageService _storageService;
    private readonly IImageProcessor _imageProcessor;
    private readonly IMediaRepository _mediaRepository;
    private readonly IMediaUrlFactory _urlFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly ProductUploadSettings _uploadSettings;
    private readonly ILogger<UploadImageCommandHandler> _logger;

    public UploadImageCommandHandler(
        IStorageService storageService,
        IImageProcessor imageProcessor,
        IMediaRepository mediaRepository,
        IMediaUrlFactory urlFactory,
        IEventPublisher eventPublisher,
        IOptions<ProductUploadSettings> uploadSettings,
        ILogger<UploadImageCommandHandler> logger)
    {
        _storageService = storageService;
        _imageProcessor = imageProcessor;
        _mediaRepository = mediaRepository;
        _urlFactory = urlFactory;
        _eventPublisher = eventPublisher;
        _uploadSettings = uploadSettings.Value;
        _logger = logger;
    }

    public async Task<UploadImageResult> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate file size
            if (request.SizeInBytes > _uploadSettings.MaxFileSizeBytes)
            {
                return new UploadImageResult(
                    Success: false,
                    ErrorMessage: $"File size exceeds maximum allowed size of {_uploadSettings.MaxFileSizeBytes / 1024 / 1024}MB");
            }

            // Validate content type
            if (!_uploadSettings.AllowedContentTypes.Contains(request.ContentType))
            {
                return new UploadImageResult(
                    Success: false,
                    ErrorMessage: $"Invalid file type. Allowed types: {string.Join(", ", _uploadSettings.AllowedContentTypes)}");
            }

            // Validate image
            request.FileStream.Position = 0;
            var isValidImage = await _imageProcessor.ValidateImageAsync(request.FileStream);
            if (!isValidImage)
            {
                return new UploadImageResult(
                    Success: false,
                    ErrorMessage: "Invalid or corrupted image file");
            }

            // Get image dimensions
            request.FileStream.Position = 0;
            var (width, height) = await _imageProcessor.GetDimensionsAsync(request.FileStream);

            // Generate unique media ID
            var mediaId = Guid.NewGuid().ToString();
            var sanitizedFileName = SanitizeFileName(request.FileName);

            // Generate S3 key for original image only
            var originalKey = string.Format(
                StorageConstants.S3PathTemplate,
                request.TenantId,
                request.EntityType.ToLower(),
                request.EntityId,
                "original",
                sanitizedFileName);

            // Upload ONLY original to S3 (variants will be generated asynchronously)
            request.FileStream.Position = 0;
            await _storageService.UploadAsync(request.FileStream, originalKey, request.ContentType, cancellationToken);

            // Create media metadata with PROCESSING status
            var media = new MediaMetadata
            {
                PK = $"TENANT#{request.TenantId}",
                SK = $"MEDIA#{mediaId}",
                GSI1PK = $"TENANT#{request.TenantId}#{request.EntityType.ToUpper()}#{request.EntityId}",
                GSI1SK = $"MEDIA#{mediaId}",
                Id = mediaId,
                TenantId = request.TenantId,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                FileName = sanitizedFileName,
                OriginalFileName = request.FileName,
                ContentType = request.ContentType,
                SizeInBytes = request.SizeInBytes,
                Width = width,
                Height = height,
                OriginalKey = originalKey,
                ThumbnailKey = string.Empty,  // Will be set by background processor
                MediumKey = string.Empty,      // Will be set by background processor
                LargeKey = string.Empty,       // Will be set by background processor
                DisplayOrder = request.DisplayOrder,
                AltText = request.AltText,
                Status = ProcessingStatus.Processing.ToString(),
                UploadedAt = DateTime.UtcNow,
                UploadedBy = request.UploadedBy,
                IsDeleted = false
            };

            // Save metadata to DynamoDB
            await _mediaRepository.CreateAsync(media);

            // Publish event for async processing
            var uploadEvent = new MediaUploadedEvent(
                MediaId: mediaId,
                TenantId: request.TenantId,
                EntityType: request.EntityType,
                EntityId: request.EntityId,
                OriginalKey: originalKey,
                ContentType: request.ContentType,
                Width: width,
                Height: height,
                UploadedAt: DateTime.UtcNow);

            await _eventPublisher.PublishAsync(uploadEvent, cancellationToken);

            // Generate URLs (only original is available now)
            var urls = _urlFactory.GetUrls(media);

            _logger.LogInformation(
                "Successfully uploaded original image {MediaId} for {EntityType} {EntityId}. Processing variants asynchronously.",
                mediaId, request.EntityType, request.EntityId);

            return new UploadImageResult(
                Success: true,
                MediaId: mediaId,
                Urls: urls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image for {EntityType} {EntityId}", request.EntityType, request.EntityId);
            return new UploadImageResult(
                Success: false,
                ErrorMessage: "Failed to upload image. Please try again.");
        }
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove any path characters and keep only the file name
        var name = Path.GetFileName(fileName);

        // Generate a unique name to prevent collisions
        var extension = Path.GetExtension(name);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(name);

        // Remove any non-alphanumeric characters except dash and underscore
        nameWithoutExt = new string(nameWithoutExt
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        // Add timestamp for uniqueness
        return $"{nameWithoutExt}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
    }
}
