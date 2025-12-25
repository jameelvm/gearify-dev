namespace Gearify.CatalogService.API.DTOs;

/// <summary>
/// DTO for Brand information
/// </summary>
public class BrandDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public int ProductCount { get; set; }
}
