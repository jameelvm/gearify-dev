using Gearify.CartService.Domain.Entities;

namespace Gearify.CartService.Infrastructure.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetCartAsync(string userId, string tenantId);
    Task SaveCartAsync(Cart cart);
    Task DeleteCartAsync(string userId, string tenantId);
}
