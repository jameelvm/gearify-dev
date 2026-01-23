using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenantContext,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CancelOrderResult> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var tenantId = request.TenantIdOverride ?? _tenantContext.TenantId;

            var order = await unitOfWork.Orders.GetByIdAsync(request.OrderId, tenantId, cancellationToken);
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
            await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus.ToString(),
                ToStatus = OrderStatus.Cancelled.ToString(),
                Reason = request.Reason,
                ChangedBy = request.CancelledBy
            }, cancellationToken);

            await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Cancelled order {OrderId} from status {FromStatus}. Reason: {Reason}",
                request.OrderId, previousStatus, request.Reason);

            // Reload to get updated status history
            await using var readUow = _unitOfWorkFactory.Create();
            order = await readUow.Orders.GetByIdAsync(request.OrderId, tenantId, cancellationToken);

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
