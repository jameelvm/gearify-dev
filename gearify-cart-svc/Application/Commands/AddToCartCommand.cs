using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(
    string UserId,
    string TenantId,
    string ProductId,
    string ProductName,
    string Sku,
    int Quantity,
    decimal Price,
    string? ImageUrl = null
) : IRequest<AddToCartResult>;

public record AddToCartResult(bool Success, Cart? Cart = null, string? ErrorMessage = null);

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ILogger<AddToCartCommandHandler> _logger;

    public AddToCartCommandHandler(ICartRepository repository, ILogger<AddToCartCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<AddToCartResult> Handle(AddToCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await _repository.GetCartAsync(request.UserId, request.TenantId)
                ?? new Cart
                {
                    UserId = request.UserId,
                    TenantId = request.TenantId,
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += request.Quantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = request.ProductId,
                    ProductName = request.ProductName,
                    Sku = request.Sku,
                    Quantity = request.Quantity,
                    Price = request.Price,
                    ImageUrl = request.ImageUrl ?? string.Empty
                });
            }

            cart.RecalculateTotal();
            cart.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCartAsync(cart);

            _logger.LogInformation("Added product {ProductId} to cart for user {UserId}", request.ProductId, request.UserId);

            return new AddToCartResult(true, cart);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add product to cart for user {UserId}", request.UserId);
            return new AddToCartResult(false, null, ex.Message);
        }
    }
}

public record RemoveFromCartCommand(
    string UserId,
    string TenantId,
    string ProductId
) : IRequest<RemoveFromCartResult>;

public record RemoveFromCartResult(bool Success, string? ErrorMessage = null);

public class RemoveFromCartCommandHandler : IRequestHandler<RemoveFromCartCommand, RemoveFromCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ILogger<RemoveFromCartCommandHandler> _logger;

    public RemoveFromCartCommandHandler(ICartRepository repository, ILogger<RemoveFromCartCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RemoveFromCartResult> Handle(RemoveFromCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await _repository.GetCartAsync(request.UserId, request.TenantId);
            if (cart == null)
            {
                return new RemoveFromCartResult(false, "Cart not found");
            }

            cart.Items.RemoveAll(i => i.ProductId == request.ProductId);
            cart.RecalculateTotal();
            cart.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCartAsync(cart);

            _logger.LogInformation("Removed product {ProductId} from cart for user {UserId}", request.ProductId, request.UserId);

            return new RemoveFromCartResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove product from cart for user {UserId}", request.UserId);
            return new RemoveFromCartResult(false, ex.Message);
        }
    }
}

public record ClearCartCommand(string UserId, string TenantId) : IRequest<ClearCartResult>;

public record ClearCartResult(bool Success, string? ErrorMessage = null);

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, ClearCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ILogger<ClearCartCommandHandler> _logger;

    public ClearCartCommandHandler(ICartRepository repository, ILogger<ClearCartCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ClearCartResult> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.DeleteCartAsync(request.UserId, request.TenantId);
            _logger.LogInformation("Cleared cart for user {UserId}", request.UserId);
            return new ClearCartResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cart for user {UserId}", request.UserId);
            return new ClearCartResult(false, ex.Message);
        }
    }
}
