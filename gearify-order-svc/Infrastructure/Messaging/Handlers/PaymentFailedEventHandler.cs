using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Commands;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Events;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Messaging;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Infrastructure.Messaging.Handlers;

/// <summary>
/// Handles PaymentFailedEvent from Payment Service.
/// Marks the order as PaymentFailed.
/// Also handles deferred cancellation when SagaState == Compensating.
/// </summary>
public class PaymentFailedEventHandler : IEventHandler<PaymentFailedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<PaymentFailedEventHandler> _logger;

    public PaymentFailedEventHandler(
        IMediator mediator,
        IUnitOfWorkFactory unitOfWorkFactory,
        ISnsEventPublisher eventPublisher,
        ILogger<PaymentFailedEventHandler> logger)
    {
        _mediator = mediator;
        _unitOfWorkFactory = unitOfWorkFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(PaymentFailedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing PaymentFailedEvent for Order {OrderId}: {ErrorMessage}",
            evt.OrderId,
            evt.ErrorMessage);

        // Check if order has a pending cancellation request (deferred cancellation)
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);
        var order = await unitOfWork.Orders.GetByIdAsync(evt.OrderId, evt.TenantId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for payment failed event", evt.OrderId);
            return true;
        }

        // ============================================================
        // DEFERRED CANCELLATION: Check if cancellation was requested
        // while payment was processing. If so, complete the cancellation.
        // No refund needed since payment failed.
        // ============================================================
        if (order.SagaState == SagaState.Compensating)
        {
            _logger.LogInformation(
                "Order {OrderId} has pending cancellation. Payment failed, completing cancellation without refund.",
                evt.OrderId);

            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.SagaState = SagaState.Failed;
            order.SagaError = $"Cancelled during payment processing. Payment failed: {evt.ErrorMessage}";

            // Add status history
            await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = OrderStatus.PaymentProcessing.ToString(),
                ToStatus = OrderStatus.Cancelled.ToString(),
                Reason = $"Deferred cancellation completed after payment failed. Reason: {order.CancellationReason}",
                ChangedBy = order.CancellationRequestedBy ?? "System"
            }, cancellationToken);

            await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            // Publish OrderCancelledEvent WITHOUT PaymentId (no refund needed)
            var cancelledEvent = new OrderCancelledEvent
            {
                TenantId = order.TenantId,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                UserId = order.UserId,
                Reason = order.CancellationReason ?? "Cancelled by user",
                CancelledBy = order.CancellationRequestedBy,
                PaymentId = null,      // No PaymentId - no refund needed
                PaidAmount = null,
                Currency = null,
                OccurredAt = DateTime.UtcNow
            };

            await _eventPublisher.PublishAsync(cancelledEvent, cancellationToken);

            _logger.LogInformation(
                "Published OrderCancelledEvent for deferred cancellation (payment failed). Order {OrderId}. No refund needed.",
                order.Id);

            return true;
        }

        // Normal flow: No pending cancellation, mark as PaymentFailed
        var command = new UpdateOrderStatusCommand(
            evt.OrderId,
            OrderStatus.PaymentFailed,
            $"Payment failed: {evt.ErrorMessage}",
            "System",
            evt.TenantId);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Order {OrderId} marked as PaymentFailed: {Error}",
                evt.OrderId, evt.ErrorMessage);
        }
        else
        {
            _logger.LogWarning("Failed to update order {OrderId} status to PaymentFailed: {Error}",
                evt.OrderId, result.ErrorMessage);
        }

        return true;
    }
}
