namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Represents a section within a category (e.g., "Shop New Arrivals", "By Brand")
/// Used for organizing the mega menu navigation
/// </summary>
public class CategorySection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CategoryId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool ShowTitle { get; set; } = true; // Controls if title is displayed in UI
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}
