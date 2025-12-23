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
);

public record CategorySectionDto(
    string Id,
    string CategoryId,
    string Title,
    string Slug,
    bool ShowTitle,
    int DisplayOrder,
    bool IsActive
);

public record SubcategoryDto(
    string Id,
    string CategoryId,
    string SectionId,
    string Name,
    string Slug,
    string Description,
    string ImageUrl,
    int DisplayOrder,
    int ProductCount,
    bool IsActive
);

public record CategoryWithDetailsDto(
    CategoryDto Category,
    List<SectionWithItemsDto> Sections
);

public record SectionWithItemsDto(
    string Id,
    string Title,
    string Slug,
    bool ShowTitle,
    int DisplayOrder,
    List<SubcategoryDto> Items
);
