using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResult>
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateCartItemCommandHandler> _logger;

    public UpdateCartItemCommandHandler(
        ICartRepository repository,
        ICartCacheService cache,
        ITenantContext tenantContext,
        ILogger<UpdateCartItemCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UpdateCartItemResult> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Check cache first, then repository
            var cart = await _cache.GetAsync(request.UserId, tenantId)
                ?? await _repository.GetCartAsync(request.UserId, tenantId);

            if (cart == null)
            {
                return new UpdateCartItemResult(false, null, "Cart not found");
            }

            var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (item == null)
            {
                return new UpdateCartItemResult(false, null, "Item not found in cart");
            }

            if (request.Quantity <= 0)
            {
                // Remove item if quantity is 0 or negative
                cart.Items.Remove(item);
                _logger.LogInformation(
                    "Removed product {ProductId} from cart (quantity set to {Quantity})",
                    request.ProductId, request.Quantity);
            }
            else
            {
                item.Quantity = request.Quantity;
                _logger.LogInformation(
                    "Updated product {ProductId} quantity to {Quantity}",
                    request.ProductId, request.Quantity);
            }

            cart.RecalculateTotal();
            cart.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCartAsync(cart);
            await _cache.SetAsync(cart);

            return new UpdateCartItemResult(true, CartMapper.ToResponse(cart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update cart item for user {UserId}", request.UserId);
            return new UpdateCartItemResult(false, null, ex.Message);
        }
    }
}
