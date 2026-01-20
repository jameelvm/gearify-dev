using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.OrderService.Application.Queries;

public class GetOrderByNumberQueryHandler : IRequestHandler<GetOrderByNumberQuery, OrderDto?>
{
    private readonly IOrderRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetOrderByNumberQueryHandler(
        IOrderRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<OrderDto?> Handle(GetOrderByNumberQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var order = await _repository.GetByOrderNumberAsync(request.OrderNumber, tenantId, cancellationToken);

        return order != null ? OrderMapper.ToDto(order) : null;
    }
}
