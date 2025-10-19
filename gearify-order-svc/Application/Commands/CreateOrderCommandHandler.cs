using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Domain.Entities;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.OrderService.Application.Commands;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository repository,
        ITenantContext tenantContext,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var order = new Order
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                UserId = request.UserId,
                Items = request.Items,
                ShippingAddress = request.ShippingAddress,
                TotalAmount = request.Items.Sum(i => i.Price * i.Quantity),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(order);

            _logger.LogInformation("Created order {OrderId} for user {UserId}", order.Id, request.UserId);

            return new CreateOrderResult(true, order.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for user {UserId}", request.UserId);
            return new CreateOrderResult(false, null, ex.Message);
        }
    }
}
