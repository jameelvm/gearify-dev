using Gearify.AuthService.Domain.Entities;

namespace Gearify.AuthService.Infrastructure.Repositories;

/// <summary>
/// Repository interface for address data access
/// </summary>
public interface IAddressRepository
{
    /// <summary>
    /// Gets all addresses for a user
    /// </summary>
    Task<IEnumerable<Address>> GetByUserIdAsync(string userId, string tenantId);

    /// <summary>
    /// Gets a specific address by ID
    /// </summary>
    Task<Address?> GetByIdAsync(string addressId, string userId, string tenantId);

    /// <summary>
    /// Gets the default address for a user
    /// </summary>
    Task<Address?> GetDefaultAsync(string userId, string tenantId);

    /// <summary>
    /// Creates a new address
    /// </summary>
    Task CreateAsync(Address address);

    /// <summary>
    /// Updates an existing address
    /// </summary>
    Task UpdateAsync(Address address);

    /// <summary>
    /// Deletes an address
    /// </summary>
    Task DeleteAsync(string addressId, string userId, string tenantId);

    /// <summary>
    /// Clears default flag from all addresses for a user
    /// </summary>
    Task ClearDefaultAsync(string userId, string tenantId);
}
