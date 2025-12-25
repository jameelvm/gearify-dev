namespace Gearify.CatalogService.Domain.Entities;

/// <summary>
/// Represents a brand/manufacturer in the catalog (e.g., SS, MRF, Kookaburra)
/// Brands are independent entities that can be referenced by products and catalog filters
/// </summary>
public class Brand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string UpdatedBy { get; set; } = string.Empty;
}
