using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(OrderDbContext context, ILogger<OrderRepository> logger)
    {
        _context = context;
        _logger = logger;
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

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.OrderNumber = Order.GenerateOrderNumber();
        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created order {OrderId} with number {OrderNumber} for tenant {TenantId}",
            order.Id, order.OrderNumber, order.TenantId);

        return order;
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        order.UpdatedAt = DateTime.UtcNow;

        _context.Orders.Update(order);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated order {OrderId} for tenant {TenantId}", order.Id, order.TenantId);
    }

    public async Task AddStatusHistoryAsync(OrderStatusHistory history, CancellationToken cancellationToken = default)
    {
        history.CreatedAt = DateTime.UtcNow;

        _context.OrderStatusHistory.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added status history for order {OrderId}: {FromStatus} -> {ToStatus}",
            history.OrderId, history.FromStatus, history.ToStatus);
    }
}
