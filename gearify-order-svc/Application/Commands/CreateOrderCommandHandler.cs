using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Events;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Multitenancy;
using Gearify.SharedKernel.Outbox;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenantContext;
    private readonly IOutboxMessageFactory _outboxMessageFactory;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenantContext,
        IOutboxMessageFactory outboxMessageFactory,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenantContext = tenantContext;
        _outboxMessageFactory = outboxMessageFactory;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);

        try
        {
            var tenantId = _tenantContext.TenantId;

            var order = new Order
            {
                TenantId = tenantId,
                UserId = request.UserId,
                Status = OrderStatus.Pending,
                Subtotal = request.Subtotal,
                TaxAmount = request.TaxAmount,
                ShippingAmount = request.ShippingAmount,
                DiscountAmount = request.DiscountAmount,
                TotalAmount = request.Subtotal + request.TaxAmount + request.ShippingAmount - request.DiscountAmount,
                Currency = request.Currency,
                ShippingAddress = OrderMapper.ToJsonDocument(request.ShippingAddress),
                ShippingAddressId = request.ShippingAddress.AddressId,
                BillingAddress = OrderMapper.ToJsonDocument(request.BillingAddress),
                BillingAddressId = request.BillingAddress?.AddressId,
                SagaState = SagaState.Created
            };

            // Add order items
            foreach (var itemRequest in request.Items)
            {
                order.Items.Add(OrderMapper.ToEntity(itemRequest, order.Id));
            }

            // Add initial status history
            order.StatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                FromStatus = OrderStatus.Pending.ToString(),
                ToStatus = OrderStatus.Pending.ToString(),
                Reason = "Order created"
            });

            var createdOrder = await unitOfWork.Orders.CreateAsync(order, cancellationToken);
            await unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation("Created order {OrderId} ({OrderNumber}) for user {UserId} in tenant {TenantId}",
                createdOrder.Id, createdOrder.OrderNumber, request.UserId, tenantId);

            // Build the event
            var orderCreatedEvent = BuildOrderCreatedEvent(request, createdOrder, tenantId);

            // Transition to PaymentProcessing + write outbox message atomically
            createdOrder = await TransitionToPaymentProcessingAsync(
                createdOrder.Id, tenantId, orderCreatedEvent, cancellationToken);

            return new CreateOrderResult(true, OrderMapper.ToDto(createdOrder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for user {UserId}", request.UserId);
            return new CreateOrderResult(false, null, ex.Message);
        }
    }

    private static OrderCreatedEvent BuildOrderCreatedEvent(CreateOrderCommand request, Order createdOrder, string tenantId)
    {
        return new OrderCreatedEvent
        {
            OrderId = createdOrder.Id,
            OrderNumber = createdOrder.OrderNumber,
            TenantId = tenantId,
            UserId = request.UserId,
            Items = createdOrder.Items.Select(i => new OrderCreatedEvent.OrderItemInfo
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList(),
            Subtotal = createdOrder.Subtotal,
            Tax = createdOrder.TaxAmount,
            ShippingCost = createdOrder.ShippingAmount,
            Total = createdOrder.TotalAmount,
            Currency = createdOrder.Currency,
            ShippingAddress = new OrderCreatedEvent.OrderAddressInfo
            {
                AddressId = request.ShippingAddress.AddressId,
                FullName = request.ShippingAddress.FullName,
                Street = request.ShippingAddress.Street,
                Street2 = request.ShippingAddress.Street2,
                City = request.ShippingAddress.City,
                State = request.ShippingAddress.State,
                PostalCode = request.ShippingAddress.PostalCode,
                Country = request.ShippingAddress.Country,
                Phone = request.ShippingAddress.Phone
            },
            BillingAddress = request.BillingAddress != null
                ? new OrderCreatedEvent.OrderAddressInfo
                {
                    AddressId = request.BillingAddress.AddressId,
                    FullName = request.BillingAddress.FullName,
                    Street = request.BillingAddress.Street,
                    Street2 = request.BillingAddress.Street2,
                    City = request.BillingAddress.City,
                    State = request.BillingAddress.State,
                    PostalCode = request.BillingAddress.PostalCode,
                    Country = request.BillingAddress.Country,
                    Phone = request.BillingAddress.Phone
                }
                : null
        };
    }

    private async Task<Order> TransitionToPaymentProcessingAsync(
        Guid orderId,
        string tenantId,
        OrderCreatedEvent orderCreatedEvent,
        CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.CreateWithTransactionAsync(cancellationToken);
        var order = await unitOfWork.Orders.GetByIdAsync(orderId, tenantId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException($"Order {orderId} not found");
        }

        order.Status = OrderStatus.PaymentProcessing;
        order.SagaState = SagaState.PaymentPending;

        await unitOfWork.Orders.AddStatusHistoryAsync(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = OrderStatus.Pending.ToString(),
            ToStatus = OrderStatus.PaymentProcessing.ToString(),
            Reason = "Payment processing initiated"
        }, cancellationToken);

        await unitOfWork.Orders.UpdateAsync(order, cancellationToken);

        // Write outbox message in same transaction as status transition
        var outbox = _outboxMessageFactory.CreateOutboxMessage(orderCreatedEvent);
        await unitOfWork.AddOutboxMessageAsync(outbox, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return order;
    }
}
