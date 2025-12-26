namespace Gearify.CatalogService.Infrastructure.Clients.DTOs;

/// <summary>
/// Media metadata DTO from Media Service
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
    string UploadedBy);
