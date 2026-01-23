using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Events;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class ConfirmOrderCommandHandler : IRequestHandler<ConfirmOrderCommand, ConfirmOrderResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ILogger<ConfirmOrderCommandHandler> _logger;

    public ConfirmOrderCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ISnsEventPublisher eventPublisher,
        ILogger<ConfirmOrderCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ConfirmOrderResult> Handle(ConfirmOrderCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var order = await unitOfWork.Orders.GetByIdAsync(request.OrderId, request.TenantId, cancellationToken);
            if (order == null)
            {
                return new ConfirmOrderResult(false, null, "Order not found");
            }

            // Check if order can be confirmed
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.PaymentProcessing)
            {
                return new ConfirmOrderResult(false, null,
                    $"Order with status {order.Status} cannot be confirmed");
            }

            var previousStatus = order.Status;
            order.Status = OrderStatus.Paid;
            order.PaymentId = request.PaymentTransactionId;
            order.PaymentStatus = "Completed";
            order.SagaState = SagaState.PaymentCompleted;

            // Add status history
            await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = previousStatus.ToString(),
                ToStatus = OrderStatus.Paid.ToString(),
                Reason = "Payment completed successfully",
                ChangedBy = "System"
            }, cancellationToken);

            await unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Confirmed order {OrderId} ({OrderNumber}) with payment {TransactionId}",
                request.OrderId, order.OrderNumber, request.PaymentTransactionId);

            // Publish OrderConfirmedEvent for shipping service
            var confirmedEvent = new OrderConfirmedEvent
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TenantId = request.TenantId,
                UserId = order.UserId,
                PaymentTransactionId = request.PaymentTransactionId
            };
            await _eventPublisher.PublishAsync(confirmedEvent, cancellationToken);

            // Reload to get updated status history
            await using var readUow = _unitOfWorkFactory.Create();
            order = await readUow.Orders.GetByIdAsync(request.OrderId, request.TenantId, cancellationToken);

            return new ConfirmOrderResult(true, OrderMapper.ToDto(order!));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm order {OrderId}", request.OrderId);
            return new ConfirmOrderResult(false, null, ex.Message);
        }
    }
}
