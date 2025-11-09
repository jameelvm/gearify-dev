using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.TenantService.Domain.Entities;

namespace Gearify.TenantService.Infrastructure.Repositories;

/// <summary>
/// Repository interface for tenant operations
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    Task<Tenant?> GetByIdAsync(string tenantId);

    /// <summary>
    /// Gets all active tenants
    /// </summary>
    Task<IEnumerable<Tenant>> GetAllActiveAsync();

    /// <summary>
    /// Gets all tenants (active and inactive)
    /// </summary>
    Task<IEnumerable<Tenant>> GetAllAsync();

    /// <summary>
    /// Creates a new tenant
    /// </summary>
    Task CreateAsync(Tenant tenant);

    /// <summary>
    /// Updates an existing tenant
    /// </summary>
    Task UpdateAsync(Tenant tenant);

    /// <summary>
    /// Deletes a tenant (soft delete - sets IsActive = false)
    /// </summary>
    Task DeleteAsync(string tenantId);

    /// <summary>
    /// Checks if a tenant exists and is active
    /// </summary>
    Task<bool> ExistsAndActiveAsync(string tenantId);
}
