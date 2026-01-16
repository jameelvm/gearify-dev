using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.API.Models;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartResponse>
{
    private readonly ICartRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCartQueryHandler(ICartRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<CartResponse> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var cart = await _repository.GetCartAsync(request.UserId, tenantId);

        return cart != null
            ? CartMapper.ToResponse(cart)
            : CartMapper.EmptyCart(request.UserId);
    }
}
