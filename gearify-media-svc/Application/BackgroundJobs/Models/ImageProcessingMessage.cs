namespace Gearify.MediaService.Application.BackgroundJobs.Models;

/// <summary>
/// Message for image processing queue
/// </summary>
public class ImageProcessingMessage
{
    public string MediaId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OriginalKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime UploadedAt { get; set; }
}
