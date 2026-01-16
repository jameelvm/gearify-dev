using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.API.Models;
using Gearify.CartService.Application.Mappers;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

public class MergeCartCommandHandler : IRequestHandler<MergeCartCommand, MergeCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MergeCartCommandHandler> _logger;
    private readonly int _userCartExpirationDays;

    public MergeCartCommandHandler(
        ICartRepository repository,
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<MergeCartCommandHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
        _userCartExpirationDays = configuration.GetValue<int>("CartConfiguration:DefaultExpirationDays", 30);
    }

    public async Task<MergeCartResult> Handle(MergeCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var guestId = request.GuestCartId.ToString();
            var userId = request.UserId.ToString();

            _logger.LogInformation(
                "Merging guest cart {GuestCartId} to user cart {UserId} with strategy {Strategy}",
                guestId, userId, request.Strategy);

            var guestCart = await _repository.GetCartAsync(guestId, tenantId);
            var userCart = await _repository.GetCartAsync(userId, tenantId);

            Cart resultCart;

            switch (request.Strategy)
            {
                case MergeStrategy.Combine:
                    resultCart = await CombineCarts(guestCart, userCart, userId, tenantId);
                    break;

                case MergeStrategy.ReplaceWithGuest:
                    resultCart = await ReplaceWithGuestCart(guestCart, userCart, userId, tenantId);
                    break;

                case MergeStrategy.KeepUser:
                    resultCart = await KeepUserCart(userCart, userId, tenantId);
                    break;

                default:
                    return new MergeCartResult(false, null, "Invalid merge strategy");
            }

            // Delete guest cart after merge
            if (guestCart != null)
            {
                await _repository.DeleteCartAsync(guestId, tenantId);
                _logger.LogInformation("Deleted guest cart {GuestCartId} after merge", guestId);
            }

            return new MergeCartResult(true, CartMapper.ToResponse(resultCart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge carts");
            return new MergeCartResult(false, null, ex.Message);
        }
    }

    private async Task<Cart> CombineCarts(Cart? guestCart, Cart? userCart, string userId, string tenantId)
    {
        // If no user cart, just transfer guest cart to user
        if (userCart == null)
        {
            if (guestCart == null)
            {
                return CreateEmptyCart(userId, tenantId);
            }

            guestCart.UserId = userId;
            guestCart.ExpiresAt = DateTime.UtcNow.AddDays(_userCartExpirationDays); // User cart TTL
            guestCart.UpdatedAt = DateTime.UtcNow;
            await _repository.SaveCartAsync(guestCart);
            return guestCart;
        }

        // If no guest cart, just return user cart
        if (guestCart == null)
        {
            return userCart;
        }

        // Combine items: merge quantities for same products
        foreach (var guestItem in guestCart.Items)
        {
            var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == guestItem.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += guestItem.Quantity;
                _logger.LogDebug(
                    "Combined quantity for product {ProductId}: {NewQuantity}",
                    guestItem.ProductId, existingItem.Quantity);
            }
            else
            {
                userCart.Items.Add(guestItem);
                _logger.LogDebug("Added product {ProductId} from guest cart", guestItem.ProductId);
            }
        }

        userCart.RecalculateTotal();
        userCart.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveCartAsync(userCart);

        return userCart;
    }

    private async Task<Cart> ReplaceWithGuestCart(Cart? guestCart, Cart? userCart, string userId, string tenantId)
    {
        // Delete user cart if exists
        if (userCart != null)
        {
            await _repository.DeleteCartAsync(userId, tenantId);
            _logger.LogDebug("Deleted user cart for ReplaceWithGuest strategy");
        }

        // If no guest cart, return empty cart
        if (guestCart == null)
        {
            var emptyCart = CreateEmptyCart(userId, tenantId);
            await _repository.SaveCartAsync(emptyCart);
            return emptyCart;
        }

        // Transfer guest cart to user
        guestCart.UserId = userId;
        guestCart.ExpiresAt = DateTime.UtcNow.AddDays(_userCartExpirationDays);
        guestCart.UpdatedAt = DateTime.UtcNow;
        await _repository.SaveCartAsync(guestCart);

        return guestCart;
    }

    private async Task<Cart> KeepUserCart(Cart? userCart, string userId, string tenantId)
    {
        // Return user cart (guest cart will be deleted by caller)
        if (userCart != null)
        {
            return userCart;
        }

        // If no user cart exists, create empty one
        var emptyCart = CreateEmptyCart(userId, tenantId);
        await _repository.SaveCartAsync(emptyCart);
        return emptyCart;
    }

    private Cart CreateEmptyCart(string userId, string tenantId)
    {
        return new Cart
        {
            UserId = userId,
            TenantId = tenantId,
            ExpiresAt = DateTime.UtcNow.AddDays(_userCartExpirationDays)
        };
    }
}
