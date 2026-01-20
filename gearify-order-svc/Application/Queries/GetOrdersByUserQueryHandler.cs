using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.OrderService.Application.DTOs;
using Gearify.OrderService.Application.Mappers;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.OrderService.Application.Queries;

public class GetOrdersByUserQueryHandler : IRequestHandler<GetOrdersByUserQuery, List<OrderSummaryDto>>
{
    private readonly IOrderRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetOrdersByUserQueryHandler(
        IOrderRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<OrderSummaryDto>> Handle(GetOrdersByUserQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var orders = await _repository.GetByUserIdAsync(request.UserId, tenantId, cancellationToken);

        return orders.Select(OrderMapper.ToSummaryDto).ToList();
    }
}
