namespace Gearify.CatalogService.API.DTOs;

/// <summary>
/// Complete mega menu structure with departments
/// Supports both single-department and multi-department tenants
/// </summary>
public class MegaMenuDto
{
    public List<DepartmentMenuDto> Departments { get; set; } = new();
}

/// <summary>
/// Department in mega menu with its categories
/// </summary>
public class DepartmentMenuDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<CategoryWithDetailsDto> Categories { get; set; } = new();
}
