using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Gearify.OrderService.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public OrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory.OrderByDescending(h => h.CreatedAt))
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber, string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistory.OrderByDescending(h => h.CreatedAt))
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber && o.TenantId == tenantId, cancellationToken);
    }

    public async Task<List<Order>> GetByUserIdAsync(string userId, string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId && o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, string tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .Where(o => o.Status == status && o.TenantId == tenantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.OrderNumber = Order.GenerateOrderNumber();
        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        _context.Orders.Add(order);

        return Task.FromResult(order);
    }

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.UpdatedAt = DateTime.UtcNow;
        _context.Orders.Update(order);

        return Task.CompletedTask;
    }

    public Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken cancellationToken = default)
    {
        history.CreatedAt = DateTime.UtcNow;
        _context.OrderStatusHistory.Add(history);

        return Task.CompletedTask;
    }
}
