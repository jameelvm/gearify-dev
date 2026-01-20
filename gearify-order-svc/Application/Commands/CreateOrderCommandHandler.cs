using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IUnitOfWorkFactory unitOfWorkFactory,
        ITenantContext tenantContext,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _tenantContext = tenantContext;
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

            return new CreateOrderResult(true, OrderMapper.ToDto(createdOrder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for user {UserId}", request.UserId);
            return new CreateOrderResult(false, null, ex.Message);
        }
    }
}
