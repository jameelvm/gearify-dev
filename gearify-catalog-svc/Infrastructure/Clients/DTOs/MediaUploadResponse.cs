namespace Gearify.CatalogService.Infrastructure.Clients.DTOs;

/// <summary>
/// Response from Media Service upload endpoint
/// </summary>
public record MediaUploadResponse(
    string MediaId,
    Dictionary<string, string> Urls);
