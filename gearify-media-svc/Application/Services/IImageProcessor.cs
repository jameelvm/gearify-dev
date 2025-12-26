using Gearify.MediaService.Domain.Enums;

namespace Gearify.MediaService.Application.Services;

/// <summary>
/// Interface for image processing operations (resize, optimize, etc.)
/// </summary>
public interface IImageProcessor
{
    /// <summary>
    /// Generate all image size variants from the original
    /// </summary>
    Task<Dictionary<ImageSize, Stream>> GenerateVariantsAsync(Stream originalStream, string contentType);

    /// <summary>
    /// Get image dimensions
    /// </summary>
    Task<(int width, int height)> GetDimensionsAsync(Stream imageStream);

    /// <summary>
    /// Validate if stream is a valid image
    /// </summary>
    Task<bool> ValidateImageAsync(Stream imageStream);
}
