using Gearify.MediaService.Application.Services;
using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Infrastructure.Constants;
using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<UploadImageCommandHandler> _logger;

    public UploadImageCommandHandler(
        IStorageService storageService,
        IImageProcessor imageProcessor,
        IMediaRepository mediaRepository,
        IMediaUrlFactory urlFactory,
        ILogger<UploadImageCommandHandler> logger)
    {
        _storageService = storageService;
        _imageProcessor = imageProcessor;
        _mediaRepository = mediaRepository;
        _urlFactory = urlFactory;
        _logger = logger;
    }

    public async Task<UploadImageResult> Handle(UploadImageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate file size
            if (request.SizeInBytes > StorageConstants.MaxFileSizeBytes)
            {
                return new UploadImageResult(
                    Success: false,
                    ErrorMessage: $"File size exceeds maximum allowed size of {StorageConstants.MaxFileSizeBytes / 1024 / 1024}MB");
            }

            // Validate content type
            if (!StorageConstants.AllowedContentTypes.Contains(request.ContentType))
            {
                return new UploadImageResult(
                    Success: false,
                    ErrorMessage: "Invalid file type. Only JPEG, PNG, and WebP images are allowed");
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
            var mediaId = $"media-{Guid.NewGuid():N}";
            var sanitizedFileName = SanitizeFileName(request.FileName);

            // Generate all image variants
            request.FileStream.Position = 0;
            var variants = await _imageProcessor.GenerateVariantsAsync(request.FileStream, request.ContentType);

            // Prepare S3 keys for each variant
            var keys = new Dictionary<ImageSize, string>();
            foreach (var size in variants.Keys)
            {
                var key = string.Format(
                    StorageConstants.S3PathTemplate,
                    request.TenantId,
                    request.EntityType.ToLower(),
                    request.EntityId,
                    size.ToString().ToLower(),
                    sanitizedFileName);

                keys[size] = key;
            }

            // Upload all variants to S3
            try
            {
                await _storageService.UploadVariantsAsync(variants, keys[ImageSize.Original], request.ContentType, cancellationToken);
            }
            finally
            {
                // Cleanup streams
                foreach (var stream in variants.Values)
                {
                    await stream.DisposeAsync();
                }
            }

            // Create media metadata
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
                OriginalKey = keys[ImageSize.Original],
                ThumbnailKey = keys[ImageSize.Thumbnail],
                MediumKey = keys[ImageSize.Medium],
                LargeKey = keys[ImageSize.Large],
                DisplayOrder = request.DisplayOrder,
                AltText = request.AltText,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = request.UploadedBy,
                IsDeleted = false
            };

            // Save metadata to DynamoDB
            await _mediaRepository.CreateAsync(media);

            // Generate URLs
            var urls = _urlFactory.GetUrls(media);

            _logger.LogInformation(
                "Successfully uploaded image {MediaId} for {EntityType} {EntityId}",
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
