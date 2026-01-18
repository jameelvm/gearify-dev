using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, ClearCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ICartCacheService _cache;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ClearCartCommandHandler> _logger;

    public ClearCartCommandHandler(
        ICartRepository repository,
        ICartCacheService cache,
        ITenantContext tenantContext,
        ILogger<ClearCartCommandHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ClearCartResult> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            await _repository.DeleteCartAsync(request.UserId, tenantId);
            await _cache.InvalidateAsync(request.UserId, tenantId);
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
