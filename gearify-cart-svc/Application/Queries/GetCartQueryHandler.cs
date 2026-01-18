using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.API.Models;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CartService.Application.Queries;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartResponse>
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly ITenantContext _tenantContext;

    public GetCartQueryHandler(
        ICartRepository repository,
        ICartCacheService cache,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _cache = cache;
        _tenantContext = tenantContext;
    }

    public async Task<CartResponse> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Check cache first
        var cart = await _cache.GetAsync(request.UserId, tenantId);
        if (cart != null)
        {
            return CartMapper.ToResponse(cart);
        }

        // Fall back to repository
        cart = await _repository.GetCartAsync(request.UserId, tenantId);
        if (cart != null)
        {
            // Populate cache
            await _cache.SetAsync(cart);
            return CartMapper.ToResponse(cart);
        }

        return CartMapper.EmptyCart(request.UserId);
    }
}
