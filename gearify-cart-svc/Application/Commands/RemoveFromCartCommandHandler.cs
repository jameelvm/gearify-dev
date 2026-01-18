using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

public class RemoveFromCartCommandHandler : IRequestHandler<RemoveFromCartCommand, RemoveFromCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RemoveFromCartCommandHandler> _logger;

    public RemoveFromCartCommandHandler(
        ICartRepository repository,
        ICartCacheService cache,
        ITenantContext tenantContext,
        ILogger<RemoveFromCartCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RemoveFromCartResult> Handle(RemoveFromCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Check cache first, then repository
            var cart = await _cache.GetAsync(request.UserId, tenantId)
                ?? await _repository.GetCartAsync(request.UserId, tenantId);

            if (cart == null)
            {
                return new RemoveFromCartResult(false, null, "Cart not found");
            }

            cart.Items.RemoveAll(i => i.ProductId == request.ProductId);

            // If cart is empty after removal, delete it entirely
            if (cart.Items.Count == 0)
            {
                await _repository.DeleteCartAsync(request.UserId, tenantId);
                await _cache.InvalidateAsync(request.UserId, tenantId);
                _logger.LogInformation("Deleted empty cart for user {UserId}", request.UserId);
                return new RemoveFromCartResult(true, CartMapper.EmptyCart(request.UserId));
            }

            cart.RecalculateTotal();
            cart.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCartAsync(cart);
            await _cache.SetAsync(cart);

            _logger.LogInformation("Removed product {ProductId} from cart for user {UserId}", request.ProductId, request.UserId);

            return new RemoveFromCartResult(true, CartMapper.ToResponse(cart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove product from cart for user {UserId}", request.UserId);
            return new RemoveFromCartResult(false, null, ex.Message);
        }
    }
}
