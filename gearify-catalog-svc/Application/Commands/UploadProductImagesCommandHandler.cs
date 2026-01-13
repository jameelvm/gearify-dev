using Gearify.CatalogService.Infrastructure.Clients;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Commands;

/// <summary>
/// Handler for uploading product images via Media Service
/// </summary>
public class UploadProductImagesCommandHandler : IRequestHandler<UploadProductImagesCommand, UploadProductImagesResult>
{
    private readonly IMediaServiceClient _mediaServiceClient;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ProductImageUploadConfiguration _uploadSettings;
    private readonly ILogger<UploadProductImagesCommandHandler> _logger;

    public UploadProductImagesCommandHandler(
        IMediaServiceClient mediaServiceClient,
        IProductRepository productRepository,
        ITenantContext tenantContext,
        IOptions<ProductImageUploadConfiguration> uploadSettings,
        ILogger<UploadProductImagesCommandHandler> logger)
    {
        _mediaServiceClient = mediaServiceClient;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
        _uploadSettings = uploadSettings.Value;
        _logger = logger;
    }

    public async Task<UploadProductImagesResult> Handle(
        UploadProductImagesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate product exists
            var product = await _productRepository.GetByIdAsync(request.ProductId, _tenantContext.TenantId);
            if (product == null)
            {
                return new UploadProductImagesResult(
                    Success: false,
                    ErrorMessage: $"Product {request.ProductId} not found");
            }

            // Validate images
            if (!request.Images.Any())
            {
                return new UploadProductImagesResult(
                    Success: false,
                    ErrorMessage: "No images provided");
            }

            // Validate each image
            foreach (var image in request.Images)
            {
                if (image.Length == 0)
                {
                    return new UploadProductImagesResult(
                        Success: false,
                        ErrorMessage: "One or more images are empty");
                }

                if (image.Length > _uploadSettings.MaxFileSizeBytes)
                {
                    return new UploadProductImagesResult(
                        Success: false,
                        ErrorMessage: $"Image {image.FileName} exceeds maximum size of {_uploadSettings.MaxFileSizeBytes / 1024 / 1024}MB");
                }

                if (!_uploadSettings.AllowedContentTypes.Contains(image.ContentType.ToLower()))
                {
                    return new UploadProductImagesResult(
                        Success: false,
                        ErrorMessage: $"Image {image.FileName} has invalid content type. Allowed: {string.Join(", ", _uploadSettings.AllowedContentTypes)}");
                }
            }

            // Upload images to Media Service
            var uploadedImages = new List<UploadedImageInfo>();
            var displayOrder = 0;

            for (int i = 0; i < request.Images.Count; i++)
            {
                var image = request.Images[i];
                var altText = request.AltTexts?.ElementAtOrDefault(i);

                using var stream = image.OpenReadStream();

                var uploadResponse = await _mediaServiceClient.UploadProductImageAsync(
                    imageStream: stream,
                    fileName: image.FileName,
                    contentType: image.ContentType,
                    sizeInBytes: image.Length,
                    productId: request.ProductId,
                    displayOrder: displayOrder++,
                    altText: altText,
                    cancellationToken: cancellationToken);

                if (uploadResponse != null)
                {
                    uploadedImages.Add(new UploadedImageInfo(
                        MediaId: uploadResponse.MediaId,
                        Urls: uploadResponse.Urls,
                        DisplayOrder: displayOrder - 1));

                    _logger.LogInformation(
                        "Uploaded image {FileName} for product {ProductId}. MediaId: {MediaId}",
                        image.FileName,
                        request.ProductId,
                        uploadResponse.MediaId);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to upload image {FileName} for product {ProductId}",
                        image.FileName,
                        request.ProductId);
                }
            }

            if (!uploadedImages.Any())
            {
                return new UploadProductImagesResult(
                    Success: false,
                    ErrorMessage: "Failed to upload any images to Media Service");
            }

            _logger.LogInformation(
                "Successfully uploaded {Count} images for product {ProductId}",
                uploadedImages.Count,
                request.ProductId);

            return new UploadProductImagesResult(
                Success: true,
                UploadedImages: uploadedImages);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error uploading images for product {ProductId}",
                request.ProductId);

            return new UploadProductImagesResult(
                Success: false,
                ErrorMessage: $"Internal error: {ex.Message}");
        }
    }
}
