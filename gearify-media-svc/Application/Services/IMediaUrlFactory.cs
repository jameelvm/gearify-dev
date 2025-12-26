using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Domain.Enums;

namespace Gearify.MediaService.Application.Services;

/// <summary>
/// Factory for generating media URLs
/// </summary>
public interface IMediaUrlFactory
{
    /// <summary>
    /// Get public URL for a specific image size
    /// </summary>
    string GetUrl(string key);

    /// <summary>
    /// Get all URLs for a media item (all sizes)
    /// </summary>
    Dictionary<string, string> GetUrls(MediaMetadata media);

    /// <summary>
    /// Get pre-signed URL for temporary access
    /// </summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiration);
}
