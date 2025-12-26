namespace Gearify.MediaService.API.DTOs;

/// <summary>
/// DTO for media metadata response
/// </summary>
public record MediaMetadataDto(
    string Id,
    string EntityType,
    string EntityId,
    string FileName,
    string OriginalFileName,
    string ContentType,
    long SizeInBytes,
    int? Width,
    int? Height,
    Dictionary<string, string> Urls,
    int DisplayOrder,
    string? AltText,
    DateTime UploadedAt,
    string UploadedBy
);
