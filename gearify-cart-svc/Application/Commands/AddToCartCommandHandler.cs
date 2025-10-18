using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Domain.Entities;
using Gearify.CartService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

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
