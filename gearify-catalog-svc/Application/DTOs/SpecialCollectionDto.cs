namespace Gearify.CatalogService.Application.DTOs;

/// <summary>
/// DTO for special product collections (Deals, Clearance, etc.)
/// Used in mega menu and product filtering
/// </summary>
public class SpecialCollectionDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string FilterAttribute { get; set; } = string.Empty;
    public string FilterType { get; set; } = string.Empty; // "Common" or "Custom"
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public string? BadgeText { get; set; }
    public string? BadgeColor { get; set; }
    public string? Description { get; set; }
}
