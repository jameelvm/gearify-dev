using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, Cart?>
{
    private readonly ICartRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCartQueryHandler(ICartRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Cart?> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        return await _repository.GetCartAsync(request.UserId, tenantId);
    }
}
