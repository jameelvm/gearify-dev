using Gearify.MediaService.Domain.Enums;
using Gearify.MediaService.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Gearify.MediaService.Application.Services;

/// <summary>
/// Image processing using ImageSharp
/// </summary>
public class ImageProcessor : IImageProcessor
{
    private readonly ProductUploadSettings _uploadSettings;
    private readonly ILogger<ImageProcessor> _logger;

    public ImageProcessor(
        IOptions<ProductUploadSettings> uploadSettings,
        ILogger<ImageProcessor> logger)
    {
        _uploadSettings = uploadSettings.Value;
        _logger = logger;
    }

    public async Task<Dictionary<ImageSize, Stream>> GenerateVariantsAsync(Stream originalStream, string contentType)
    {
        var variants = new Dictionary<ImageSize, Stream>();

        try
        {
            // Copy original stream to memory (handles non-seekable streams from S3)
            using var memoryStream = new MemoryStream();
            await originalStream.CopyToAsync(memoryStream);
            var originalBytes = memoryStream.ToArray();

            // Original (just copy)
            var originalCopy = new MemoryStream(originalBytes);
            variants[ImageSize.Original] = originalCopy;

            // Thumbnail (configurable size)
            using (var imageStream = new MemoryStream(originalBytes))
            using (var image = await Image.LoadAsync(imageStream))
            {
                variants[ImageSize.Thumbnail] = await ResizeImageAsync(
                    image,
                    _uploadSettings.ImageSizes.ThumbnailSize,
                    _uploadSettings.ImageQuality.Thumbnail,
                    contentType);
            }

            // Medium (configurable size)
            using (var imageStream = new MemoryStream(originalBytes))
            using (var image = await Image.LoadAsync(imageStream))
            {
                variants[ImageSize.Medium] = await ResizeImageAsync(
                    image,
                    _uploadSettings.ImageSizes.MediumSize,
                    _uploadSettings.ImageQuality.Medium,
                    contentType);
            }

            // Large (configurable size)
            using (var imageStream = new MemoryStream(originalBytes))
            using (var image = await Image.LoadAsync(imageStream))
            {
                variants[ImageSize.Large] = await ResizeImageAsync(
                    image,
                    _uploadSettings.ImageSizes.LargeSize,
                    _uploadSettings.ImageQuality.Large,
                    contentType);
            }

            _logger.LogInformation("Generated {Count} image variants", variants.Count);
            return variants;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating image variants");

            // Cleanup streams on error
            foreach (var stream in variants.Values)
            {
                await stream.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<(int width, int height)> GetDimensionsAsync(Stream imageStream)
    {
        try
        {
            imageStream.Position = 0;
            var imageInfo = await Image.IdentifyAsync(imageStream);
            imageStream.Position = 0;

            return (imageInfo.Width, imageInfo.Height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting image dimensions");
            throw;
        }
    }

    public async Task<bool> ValidateImageAsync(Stream imageStream)
    {
        try
        {
            imageStream.Position = 0;
            var imageInfo = await Image.IdentifyAsync(imageStream);
            imageStream.Position = 0;

            // Basic validation - if we can identify it, it's a valid image
            return imageInfo != null && imageInfo.Width > 0 && imageInfo.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<Stream> ResizeImageAsync(Image image, int maxSize, int quality, string contentType)
    {
        // Calculate resize dimensions maintaining aspect ratio
        var width = image.Width;
        var height = image.Height;

        if (width > maxSize || height > maxSize)
        {
            var ratio = Math.Min((double)maxSize / width, (double)maxSize / height);
            width = (int)(width * ratio);
            height = (int)(height * ratio);
        }

        // Resize with high quality
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        var outputStream = new MemoryStream();

        // Save with appropriate encoder based on content type
        if (contentType.Contains("png"))
        {
            var encoder = new PngEncoder();
            await image.SaveAsync(outputStream, encoder);
        }
        else if (contentType.Contains("webp"))
        {
            var encoder = new WebpEncoder { Quality = quality };
            await image.SaveAsync(outputStream, encoder);
        }
        else
        {
            var encoder = new JpegEncoder { Quality = quality };
            await image.SaveAsync(outputStream, encoder);
        }

        outputStream.Position = 0;
        return outputStream;
    }
}
