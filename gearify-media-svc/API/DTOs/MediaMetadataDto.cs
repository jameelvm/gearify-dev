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
    string Status,              // Processing status (Processing, Ready, Failed)
    DateTime? ProcessedAt,       // When variant processing completed
    string? ProcessingError,     // Error message if processing failed
    DateTime UploadedAt,
    string UploadedBy
);
