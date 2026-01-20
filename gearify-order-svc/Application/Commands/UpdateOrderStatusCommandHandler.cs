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

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, UpdateOrderStatusResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateOrderStatusCommandHandler> _logger;

    public UpdateOrderStatusCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenantContext,
        ILogger<UpdateOrderStatusCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UpdateOrderStatusResult> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var tenantId = _tenantContext.TenantId;

            var order = await unitOfWork.Orders.GetByIdAsync(request.OrderId, tenantId, cancellationToken);
            if (order == null)
            {
                return new UpdateOrderStatusResult(false, null, "Order not found");
            }

            var previousStatus = order.Status;

            // Validate status transition
            if (!IsValidStatusTransition(previousStatus, request.NewStatus))
            {
                return new UpdateOrderStatusResult(false, null,
                    $"Invalid status transition from {previousStatus} to {request.NewStatus}");
            }

            order.Status = request.NewStatus;

            // Add status history
            await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus.ToString(),
                ToStatus = request.NewStatus.ToString(),
                Reason = request.Reason,
                ChangedBy = request.ChangedBy
            }, cancellationToken);

            switch (request.NewStatus)
            {
                // Update timestamps for terminal statuses
                case OrderStatus.Delivered:
                    order.CompletedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Cancelled:
                    order.CancelledAt = DateTime.UtcNow;
                    break;
            }

            await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Updated order {OrderId} status from {FromStatus} to {ToStatus}",
                request.OrderId, previousStatus, request.NewStatus);

            // Reload to get updated status history
            await using var readUow = _unitOfWorkFactory.Create();
            order = await readUow.Orders.GetByIdAsync(request.OrderId, tenantId, cancellationToken);

            return new UpdateOrderStatusResult(true, OrderMapper.ToDto(order!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order {OrderId} status", request.OrderId);
            return new UpdateOrderStatusResult(false, null, ex.Message);
        }
    }

    private static bool IsValidStatusTransition(OrderStatus from, OrderStatus to)
    {
        return (from, to) switch
        {
            (OrderStatus.Pending, OrderStatus.PaymentProcessing) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.Paid) => true,
            (OrderStatus.PaymentProcessing, OrderStatus.PaymentFailed) => true,
            (OrderStatus.PaymentFailed, OrderStatus.PaymentProcessing) => true,
            (OrderStatus.PaymentFailed, OrderStatus.Cancelled) => true,
            (OrderStatus.Paid, OrderStatus.Processing) => true,
            (OrderStatus.Paid, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Processing, OrderStatus.Cancelled) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            (OrderStatus.Delivered, OrderStatus.Refunded) => true,
            _ => false
        };
    }
}
