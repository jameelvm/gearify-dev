using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface IDepartmentRepository
{
    /// <summary>
    /// Get all departments for a tenant
    /// </summary>
    Task<List<Department>> GetAllAsync(string tenantId);

    /// <summary>
    /// Get a department by its ID
    /// </summary>
    Task<Department?> GetByIdAsync(string departmentId, string tenantId);

    /// <summary>
    /// Get a department by its slug
    /// </summary>
    Task<Department?> GetBySlugAsync(string slug, string tenantId);

    /// <summary>
    /// Get categories for a specific department
    /// </summary>
    Task<List<Category>> GetCategoriesAsync(string departmentSlug, string tenantId);
}
