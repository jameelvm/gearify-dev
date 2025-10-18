using System;
using System.Threading;
using System.Threading.Tasks;
using Gearify.CartService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CartService.Application.Commands;

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
