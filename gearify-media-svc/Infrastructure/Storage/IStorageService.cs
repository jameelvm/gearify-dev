using Gearify.MediaService.Domain.Enums;

namespace Gearify.MediaService.Infrastructure.Storage;

/// <summary>
/// Interface for cloud storage operations (S3, Azure Blob, etc.)
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Upload a file to storage
    /// </summary>
    Task<string> UploadAsync(Stream fileStream, string key, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload multiple files (for image variants)
    /// </summary>
    Task<Dictionary<ImageSize, string>> UploadVariantsAsync(
        Dictionary<ImageSize, Stream> variants,
        string baseKey,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a file from storage
    /// </summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple files
    /// </summary>
    Task DeleteMultipleAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a file from storage
    /// </summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a file exists
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a public URL for a file
    /// </summary>
    string GetPublicUrl(string key);

    /// <summary>
    /// Generate a pre-signed URL for temporary access
    /// </summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default);
}
