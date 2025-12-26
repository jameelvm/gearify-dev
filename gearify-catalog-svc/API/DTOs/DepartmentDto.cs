namespace Gearify.CatalogService.API.DTOs;

/// <summary>
/// DTO for Department entity
/// </summary>
public class DepartmentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int CategoryCount { get; set; } // Number of categories in this department
}
