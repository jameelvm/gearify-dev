using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;

namespace Gearify.OrderService.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, string tenantId, CancellationToken cancellationToken = default);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, string tenantId, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByUserIdAsync(string userId, string tenantId, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByStatusAsync(OrderStatus status, string tenantId, CancellationToken cancellationToken = default);
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
    Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken cancellationToken = default);
}
