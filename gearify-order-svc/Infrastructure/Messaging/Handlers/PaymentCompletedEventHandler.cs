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
/// Handles PaymentCompletedEvent from Payment Service.
/// Confirms the order after successful payment.
/// Also handles deferred cancellation when SagaState == Compensating.
/// </summary>
public class PaymentCompletedEventHandler : IEventHandler<PaymentCompletedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<PaymentCompletedEventHandler> _logger;

    public PaymentCompletedEventHandler(
        IMediator mediator,
        IUnitOfWorkFactory unitOfWorkFactory,
        ISnsEventPublisher eventPublisher,
        ILogger<PaymentCompletedEventHandler> logger)
    {
        _mediator = mediator;
        _unitOfWorkFactory = unitOfWorkFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(PaymentCompletedEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing PaymentCompletedEvent for Order {OrderId}, Transaction {TransactionId}",
            evt.OrderId,
            evt.TransactionId);

        // Check if order has a pending cancellation request (deferred cancellation)
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);
        var order = await unitOfWork.Orders.GetByIdAsync(evt.OrderId, evt.TenantId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found for payment completed event", evt.OrderId);
            return true;
        }

        // ============================================================
        // DEFERRED CANCELLATION: Check if cancellation was requested
        // while payment was processing. If so, complete the cancellation
        // and trigger a refund since payment succeeded.
        // ============================================================
        if (order.SagaState == SagaState.Compensating)
        {
            _logger.LogInformation(
                "Order {OrderId} has pending cancellation. Payment succeeded, completing cancellation with refund.",
                evt.OrderId);

            // Update order with PaymentId from the completed payment
            order.PaymentId = evt.TransactionId;
            order.PaymentStatus = PaymentStatus.Completed;
            order.Status = OrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            // Add status history
            await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = OrderStatus.PaymentProcessing.ToString(),
                ToStatus = OrderStatus.Cancelled.ToString(),
                Reason = $"Deferred cancellation completed after payment succeeded. Reason: {order.CancellationReason}",
                ChangedBy = order.CancellationRequestedBy ?? "System"
            }, cancellationToken);

            await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            // Publish OrderCancelledEvent WITH PaymentId to trigger refund
            var cancelledEvent = new OrderCancelledEvent
            {
                TenantId = order.TenantId,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                UserId = order.UserId,
                Reason = order.CancellationReason ?? "Cancelled by user",
                CancelledBy = order.CancellationRequestedBy,
                PaymentId = evt.TransactionId,     // Include PaymentId to trigger refund
                PaidAmount = order.TotalAmount,    // Full amount for refund
                Currency = order.Currency,
                OccurredAt = DateTime.UtcNow
            };

            await _eventPublisher.PublishAsync(cancelledEvent, cancellationToken);

            _logger.LogInformation(
                "Published OrderCancelledEvent for deferred cancellation. Order {OrderId}, PaymentId {PaymentId}. Refund will be processed.",
                order.Id, evt.TransactionId);

            return true;
        }

        // Normal flow: No pending cancellation, confirm the order
        var command = new ConfirmOrderCommand(
            evt.OrderId,
            evt.TenantId,
            evt.TransactionId);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Order {OrderId} confirmed after payment {TransactionId}",
                evt.OrderId, evt.TransactionId);
        }
        else
        {
            _logger.LogWarning("Failed to confirm order {OrderId}: {Error}",
                evt.OrderId, result.ErrorMessage);
        }

        return true;
    }
}
