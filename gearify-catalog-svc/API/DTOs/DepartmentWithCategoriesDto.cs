namespace Gearify.CatalogService.API.DTOs;

/// <summary>
/// DTO for Department with its categories
/// Used for department detail pages
/// </summary>
public class DepartmentWithCategoriesDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<CategorySummaryDto> Categories { get; set; } = new();
}

/// <summary>
/// Summary DTO for Category (without sections/subcategories)
/// </summary>
public class CategorySummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
