using Gearify.CatalogService.Infrastructure.Clients.DTOs;

namespace Gearify.CatalogService.Infrastructure.Clients;

/// <summary>
/// Client for communicating with Media Service
/// </summary>
public interface IMediaServiceClient
{
    /// <summary>
    /// Upload an image for a product
    /// </summary>
    Task<MediaUploadResponse?> UploadProductImageAsync(
        Stream imageStream,
        string fileName,
        string contentType,
        long sizeInBytes,
        string productId,
        int displayOrder = 0,
        string? altText = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all images for a product
    /// </summary>
    Task<List<MediaMetadataDto>> GetProductImagesAsync(
        string productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an image by ID
    /// </summary>
    Task<bool> DeleteImageAsync(
        string mediaId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all images for a product
    /// </summary>
    Task<bool> DeleteProductImagesAsync(
        string productId,
        bool hardDelete = false,
        CancellationToken cancellationToken = default);
}
