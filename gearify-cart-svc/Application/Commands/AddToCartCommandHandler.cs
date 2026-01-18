using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Clients;
using Gearify.CartService.Infrastructure.Configuration;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.CartService.Application.Commands;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly ICatalogServiceClient _catalogClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AddToCartCommandHandler> _logger;
    private readonly CartConfiguration _cartConfig;

    public AddToCartCommandHandler(
        ICartRepository repository,
        ICartCacheService cache,
        ICatalogServiceClient catalogClient,
        ITenantContext tenantContext,
        IOptions<CartConfiguration> cartConfig,
        ILogger<AddToCartCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _catalogClient = catalogClient;
        _tenantContext = tenantContext;
        _logger = logger;
        _cartConfig = cartConfig.Value;
    }

    public async Task<AddToCartResult> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Validate product exists and is active
            var product = await _catalogClient.GetProductAsync(request.ProductId, cancellationToken);
            if (product == null)
            {
                _logger.LogWarning("Product {ProductId} not found when adding to cart", request.ProductId);
                return new AddToCartResult(false, null, "Product not found");
            }

            if (!product.IsActive)
            {
                _logger.LogWarning("Product {ProductId} is not active", request.ProductId);
                return new AddToCartResult(false, null, "Product is not available");
            }

            // Check cache first, then repository
            var cart = await _cache.GetAsync(request.UserId, tenantId)
                ?? await _repository.GetCartAsync(request.UserId, tenantId)
                ?? new Cart
                {
                    UserId = request.UserId,
                    TenantId = tenantId,
                    ExpiresAt = DateTime.UtcNow.AddDays(_cartConfig.GuestCartExpirationDays)
                };

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
                // Update price to current catalog price
                existingItem.Price = product.Price;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Quantity = request.Quantity,
                    Price = product.Price,
                    ImageUrl = product.ThumbnailUrl ?? (product.ImageUrls.Count > 0 ? product.ImageUrls[0] : string.Empty)
                });
            }

            cart.RecalculateTotal();
            cart.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCartAsync(cart);
            await _cache.SetAsync(cart);

            _logger.LogInformation("Added product {ProductId} to cart for user {UserId}", request.ProductId, request.UserId);

            return new AddToCartResult(true, CartMapper.ToResponse(cart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add product to cart for user {UserId}", request.UserId);
            return new AddToCartResult(false, null, ex.Message);
        }
    }
}
