using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface ICategoryRepository
{
    // Get all categories with their details in parallel (optimized for mega-menu)
    Task<List<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>>
        GetAllCategoriesWithDetailsAsync(string tenantId);
}
