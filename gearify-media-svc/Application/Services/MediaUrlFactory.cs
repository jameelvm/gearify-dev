using Gearify.MediaService.Domain.Entities;
using Gearify.MediaService.Infrastructure.Storage;

namespace Gearify.MediaService.Application.Services;

/// <summary>
/// Factory for generating media URLs from S3 keys
/// </summary>
public class MediaUrlFactory : IMediaUrlFactory
{
    private readonly IStorageService _storageService;

    public MediaUrlFactory(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public string GetUrl(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        return _storageService.GetPublicUrl(key);
    }

    public Dictionary<string, string> GetUrls(MediaMetadata media)
    {
        var urls = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(media.OriginalKey))
            urls["original"] = GetUrl(media.OriginalKey);

        if (!string.IsNullOrEmpty(media.ThumbnailKey))
            urls["thumbnail"] = GetUrl(media.ThumbnailKey);

        if (!string.IsNullOrEmpty(media.MediumKey))
            urls["medium"] = GetUrl(media.MediumKey);

        if (!string.IsNullOrEmpty(media.LargeKey))
            urls["large"] = GetUrl(media.LargeKey);

        return urls;
    }

    public async Task<string> GetPresignedUrlAsync(string key, TimeSpan expiration)
    {
        return await _storageService.GetPresignedUrlAsync(key, expiration);
    }
}
