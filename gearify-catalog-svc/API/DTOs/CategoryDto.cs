using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.API.DTOs;

public record CategoryDto(
    string Id,
    string Name,
    string Slug,
    string Description,
    string Icon,
    string ImageUrl,
    int DisplayOrder,
    bool IsActive
)
{
    /// <summary>
    /// Creates DTO from domain entity
    /// </summary>
    public static CategoryDto FromEntity(Category category)
    {
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.Icon,
            category.ImageUrl,
            category.DisplayOrder,
            category.IsActive
        );
    }

    /// <summary>
    /// Creates list of DTOs from domain entities
    /// </summary>
    public static List<CategoryDto> FromEntities(IEnumerable<Category> categories)
    {
        return categories.Select(FromEntity).ToList();
    }
}

public record CategorySectionDto(
    string Id,
    string CategoryId,
    string Title,
    string Slug,
    bool ShowTitle,
    int DisplayOrder,
    bool IsActive
)
{
    /// <summary>
    /// Creates DTO from domain entity
    /// </summary>
    public static CategorySectionDto FromEntity(CategorySection section)
    {
        return new CategorySectionDto(
            section.Id,
            section.CategoryId,
            section.Title,
            section.Slug,
            section.ShowTitle,
            section.DisplayOrder,
            section.IsActive
        );
    }
}

public record SubcategoryDto(
    string Id,
    string CategoryId,
    string SectionId,
    string Name,
    string Slug,
    string Description,
    string ImageUrl,
    string? BrandId,
    string? PriceRangeId,
    string? FilterType,
    decimal? MinPrice,
    decimal? MaxPrice,
    int DisplayOrder,
    int ProductCount,
    bool IsActive
)
{
    /// <summary>
    /// Creates DTO from domain entity
    /// </summary>
    public static SubcategoryDto FromEntity(Subcategory subcategory)
    {
        return new SubcategoryDto(
            subcategory.Id,
            subcategory.CategoryId,
            subcategory.SectionId,
            subcategory.Name,
            subcategory.Slug,
            subcategory.Description,
            subcategory.ImageUrl,
            subcategory.BrandId,
            subcategory.PriceRangeId,
            subcategory.FilterType,
            subcategory.MinPrice,
            subcategory.MaxPrice,
            subcategory.DisplayOrder,
            subcategory.ProductCount,
            subcategory.IsActive
        );
    }

    /// <summary>
    /// Creates list of DTOs from domain entities
    /// </summary>
    public static List<SubcategoryDto> FromEntities(IEnumerable<Subcategory> subcategories)
    {
        return subcategories.Select(FromEntity).ToList();
    }
}

public record CategoryWithDetailsDto(
    CategoryDto Category,
    List<SectionWithItemsDto> Sections
)
{
    /// <summary>
    /// Creates DTO from domain entities with complex mapping
    /// </summary>
    public static CategoryWithDetailsDto FromEntities(
        Category category,
        List<CategorySection> sections,
        List<Subcategory> subcategories)
    {
        var sectionsWithItems = sections
            .Select(section => SectionWithItemsDto.FromEntity(section, subcategories))
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        return new CategoryWithDetailsDto(
            CategoryDto.FromEntity(category),
            sectionsWithItems
        );
    }
}

public record SectionWithItemsDto(
    string Id,
    string Title,
    string Slug,
    bool ShowTitle,
    int DisplayOrder,
    List<SubcategoryDto> Items
)
{
    /// <summary>
    /// Creates DTO from section entity with filtered subcategories
    /// </summary>
    public static SectionWithItemsDto FromEntity(
        CategorySection section,
        List<Subcategory> allSubcategories)
    {
        var items = allSubcategories
            .Where(sub => sub.SectionId == section.Id)
            .Select(SubcategoryDto.FromEntity)
            .OrderBy(sub => sub.DisplayOrder)
            .ToList();

        return new SectionWithItemsDto(
            section.Id,
            section.Title,
            section.Slug,
            section.ShowTitle,
            section.DisplayOrder,
            items
        );
    }
}
