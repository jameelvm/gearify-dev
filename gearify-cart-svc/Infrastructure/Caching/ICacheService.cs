using System;
using System.Threading.Tasks;

namespace Gearify.CartService.Infrastructure.Caching;

/// <summary>
/// Generic cache service interface
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get item from cache
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// Set item in cache with expiration
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Remove item from cache
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Check if key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key);
}
