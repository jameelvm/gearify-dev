using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface ICategoryRepository
{
    // Category operations
    Task<Category?> GetCategoryByIdAsync(string categoryId, string tenantId);
    Task<Category?> GetCategoryBySlugAsync(string slug, string tenantId);
    Task<List<Category>> GetAllCategoriesAsync(string tenantId);
    Task CreateCategoryAsync(Category category);
    Task UpdateCategoryAsync(Category category);
    Task DeleteCategoryAsync(string categoryId, string tenantId);

    // Section operations
    Task<CategorySection?> GetSectionByIdAsync(string sectionId, string categoryId, string tenantId);
    Task<List<CategorySection>> GetSectionsByCategoryAsync(string categoryId, string tenantId);
    Task CreateSectionAsync(CategorySection section);
    Task UpdateSectionAsync(CategorySection section);
    Task DeleteSectionAsync(string sectionId, string categoryId, string tenantId);

    // Subcategory operations
    Task<Subcategory?> GetSubcategoryByIdAsync(string subcategoryId, string categoryId, string tenantId);
    Task<List<Subcategory>> GetSubcategoriesBySectionAsync(string sectionId, string categoryId, string tenantId);
    Task CreateSubcategoryAsync(Subcategory subcategory);
    Task UpdateSubcategoryAsync(Subcategory subcategory);
    Task DeleteSubcategoryAsync(string subcategoryId, string categoryId, string tenantId);

    // Get complete category with sections and subcategories
    Task<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
        GetCategoryWithDetailsAsync(string categoryId, string tenantId);
}
