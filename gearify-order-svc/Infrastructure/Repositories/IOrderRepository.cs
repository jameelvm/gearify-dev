using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;

namespace Gearify.OrderService.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string orderId, string tenantId);
    Task<List<Order>> GetByUserIdAsync(string userId, string tenantId);
    Task CreateAsync(Order order);
    Task UpdateAsync(Order order);
}
