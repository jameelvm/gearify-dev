using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Events;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenantContext,
        ISnsEventPublisher eventPublisher,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenantContext = tenantContext;
        _eventPublisher = eventPublisher;
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
                return CancelOrderResult.Failed("Order not found");
            }

            // Check if order can be cancelled
            if (!CanBeCancelled(order.Status))
            {
                return CancelOrderResult.Failed(
                    $"Order with status {order.Status} cannot be cancelled. " +
                    "Only orders in Pending, PaymentProcessing, PaymentFailed, Paid, or Processing status can be cancelled.");
            }

            var previousStatus = order.Status;

            // ============================================================
            // RACE CONDITION HANDLING: PaymentProcessing status
            // ============================================================
            // When order is in PaymentProcessing, we can't cancel immediately
            // because payment might succeed at the provider (e.g., Stripe).
            // Instead, we mark it for deferred cancellation and wait for
            // the payment result to arrive via PaymentEventHandler.
            // ============================================================
            if (previousStatus == OrderStatus.PaymentProcessing)
            {
                return await HandleDeferredCancellationAsync(
                    order, request, unitOfWork, tenantId, cancellationToken);
            }

            // Check if order was paid (requires refund)
            var wasPaid = previousStatus == OrderStatus.Paid ||
                          previousStatus == OrderStatus.Processing;

            // Update order status
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = request.Reason;
            order.CancellationRequestedBy = request.CancelledBy;
            order.SagaState = wasPaid ? SagaState.Compensating : SagaState.Failed;

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

            _logger.LogInformation(
                "Cancelled order {OrderId} from status {FromStatus}. Reason: {Reason}. RefundRequired: {RefundRequired}",
                request.OrderId, previousStatus, request.Reason, wasPaid);

            // Publish OrderCancelledEvent
            await PublishOrderCancelledEventAsync(order, request.Reason, request.CancelledBy, wasPaid, cancellationToken);

            // Reload to get updated status history
            await using var readUow = _unitOfWorkFactory.Create();
            order = await readUow.Orders.GetByIdAsync(request.OrderId, tenantId, cancellationToken);

            return CancelOrderResult.Succeeded(OrderMapper.ToDto(order!), wasPaid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", request.OrderId);
            return CancelOrderResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Handle cancellation request when order is in PaymentProcessing status.
    /// We can't cancel immediately because payment might succeed at the provider.
    /// Instead, mark it for deferred cancellation and wait for payment result.
    /// </summary>
    private async Task<CancelOrderResult> HandleDeferredCancellationAsync(
        Order order,
        CancelOrderCommand request,
        IUnitOfWork unitOfWork,
        string tenantId,
        CancellationToken cancellationToken)
    {
        // Mark order for deferred cancellation
        // DON'T change OrderStatus - keep it as PaymentProcessing
        // The PaymentEventHandler will check SagaState and complete the cancellation
        order.SagaState = SagaState.Compensating;
        order.CancellationReason = request.Reason;
        order.CancellationRequestedBy = request.CancelledBy;
        order.CancellationRequestedAt = DateTime.UtcNow;

        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Deferred cancellation requested for order {OrderId}. Status: {Status}. " +
            "Waiting for payment result before completing cancellation.",
            order.Id, order.Status);

        // Reload to get current state
        await using var readUow = _unitOfWorkFactory.Create();
        order = await readUow.Orders.GetByIdAsync(order.Id, tenantId, cancellationToken);

        return CancelOrderResult.Pending(
            OrderMapper.ToDto(order!),
            "Cancellation requested. Payment is currently being processed. " +
            "If the payment succeeds, we will automatically process a refund. " +
            "You will receive an email confirmation once the cancellation is complete.");
    }

    /// <summary>
    /// Publish OrderCancelledEvent to trigger downstream actions (refund, notification).
    /// </summary>
    private async Task PublishOrderCancelledEventAsync(
        Order order,
        string reason,
        string? cancelledBy,
        bool wasPaid,
        CancellationToken cancellationToken)
    {
        var evt = new OrderCancelledEvent
        {
            TenantId = order.TenantId,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            Reason = reason,
            CancelledBy = cancelledBy,
            PaymentId = wasPaid ? order.PaymentId : null,
            PaidAmount = wasPaid ? order.TotalAmount : null,
            Currency = wasPaid ? order.Currency : null,
            OccurredAt = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(evt, cancellationToken);

        _logger.LogInformation(
            "Published OrderCancelledEvent for order {OrderId}. PaymentId: {PaymentId}",
            order.Id, evt.PaymentId);
    }

    private static bool CanBeCancelled(OrderStatus status)
    {
        //Before all the status before shipping is cancellable/reversible 
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
