using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IOrderRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderRepository repository,
        ITenantContext tenantContext,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CancelOrderResult> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var order = await _repository.GetByIdAsync(request.OrderId, tenantId, cancellationToken);
            if (order == null)
            {
                return new CancelOrderResult(false, null, "Order not found");
            }

            // Check if order can be cancelled
            if (!CanBeCancelled(order.Status))
            {
                return new CancelOrderResult(false, null,
                    $"Order with status {order.Status} cannot be cancelled");
            }

            var previousStatus = order.Status;
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;

            // Add status history
            await _repository.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus.ToString(),
                ToStatus = OrderStatus.Cancelled.ToString(),
                Reason = request.Reason,
                ChangedBy = request.CancelledBy
            }, cancellationToken);

            await _repository.UpdateAsync(order, cancellationToken);

            _logger.LogInformation("Cancelled order {OrderId} from status {FromStatus}. Reason: {Reason}",
                request.OrderId, previousStatus, request.Reason);

            // Reload to get updated status history
            order = await _repository.GetByIdAsync(request.OrderId, tenantId, cancellationToken);

            return new CancelOrderResult(true, OrderMapper.ToDto(order!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", request.OrderId);
            return new CancelOrderResult(false, null, ex.Message);
        }
    }

    private static bool CanBeCancelled(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => true,
            OrderStatus.PaymentProcessing => true,
            OrderStatus.PaymentFailed => true,
            OrderStatus.Paid => true,
            OrderStatus.Processing => true,
            _ => false
        };
    }
}
