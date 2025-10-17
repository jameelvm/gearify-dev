using Gearify.CatalogService.Domain.Entities;

namespace Gearify.CatalogService.Infrastructure.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string productId, string tenantId);
    Task<List<Product>> GetAllAsync(string tenantId, int skip = 0, int take = 50);
    Task<List<Product>> GetByCategoryAsync(string category, string tenantId);
    Task CreateAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(string productId, string tenantId);
}
