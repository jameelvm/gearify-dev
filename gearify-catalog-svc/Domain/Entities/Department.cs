namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Represents a top-level department/industry in the catalog (e.g., Cricket, Perfume, Electronics)
/// Used to organize categories by business vertical for multi-department marketplaces
/// </summary>
public class Department
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty; // e.g., "Cricket", "Perfume", "Electronics"
    public string Slug { get; set; } = string.Empty; // e.g., "cricket", "perfume", "electronics"
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty; // Icon identifier for UI
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}
