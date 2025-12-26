using MediatR;

namespace Gearify.MediaService.Application.Commands;

/// <summary>
/// Command to upload an image
/// </summary>
public record UploadImageCommand(
    Stream FileStream,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string EntityType,
    string EntityId,
    string TenantId,
    string UploadedBy,
    int DisplayOrder = 0,
    string? AltText = null
) : IRequest<UploadImageResult>;

/// <summary>
/// Result of image upload
/// </summary>
public record UploadImageResult(
    bool Success,
    string? MediaId = null,
    Dictionary<string, string>? Urls = null,
    string? ErrorMessage = null
);
