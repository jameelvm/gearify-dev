using System.ComponentModel.DataAnnotations;

namespace Gearify.MediaService.API.DTOs;

/// <summary>
/// Request for uploading an image
/// Note: This is used for documentation. Actual upload uses IFormFile
/// </summary>
public record UploadImageRequest(
    [Required] string EntityType,
    [Required] string EntityId,
    int DisplayOrder = 0,
    string? AltText = null
);
