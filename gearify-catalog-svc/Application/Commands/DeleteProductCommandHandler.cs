using Gearify.CatalogService.Domain.Events;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand, DeleteProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteProductCommandHandler> _logger;

    public DeleteProductCommandHandler(
        IProductRepository repository,
        ISnsEventPublisher eventPublisher,
        ITenantContext tenantContext,
        ILogger<DeleteProductCommandHandler> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DeleteProductResult> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            // Verify product exists before deleting
            var product = await _repository.GetByIdAsync(request.ProductId, tenantId);
            if (product == null)
            {
                return new DeleteProductResult(false, "Product not found");
            }

            // Delete from repository
            await _repository.DeleteAsync(request.ProductId, tenantId);

            _logger.LogInformation("Product deleted: {ProductId} for tenant {TenantId}", request.ProductId, tenantId);

            // Publish event for Search Service to remove from index
            await _eventPublisher.PublishAsync(
                new ProductDeletedEvent(request.ProductId, tenantId, DateTime.UtcNow),
                cancellationToken);

            return new DeleteProductResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete product {ProductId}", request.ProductId);
            return new DeleteProductResult(false, ex.Message);
        }
    }
}
