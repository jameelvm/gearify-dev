using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

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
