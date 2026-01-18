using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;

namespace Gearify.CartService.Infrastructure.Caching;

/// <summary>
/// Cart-specific caching service
/// </summary>
public interface ICartCacheService
{
    Task<Cart?> GetAsync(string userId, string tenantId);
    Task SetAsync(Cart cart);
    Task InvalidateAsync(string userId, string tenantId);
}
