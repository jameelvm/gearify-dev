using Gearify.CatalogService.Domain.Events;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductRepository repository,
        ISnsEventPublisher eventPublisher,
        ITenantContext tenantContext,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<UpdateProductResult> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;
            var product = await _repository.GetByIdAsync(request.ProductId, tenantId);
            if (product == null)
            {
                return new UpdateProductResult(false, "Product not found");
            }

            if (request.Name != null) product.Name = request.Name;
            if (request.Description != null) product.Description = request.Description;
            if (request.Price.HasValue) product.Price = request.Price.Value;
            if (request.CompareAtPrice.HasValue) product.CompareAtPrice = request.CompareAtPrice.Value;
            if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;

            product.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(product);

            _logger.LogInformation("Product updated: {ProductId}", product.Id);

            // Publish event for Search Service to update the index
            await _eventPublisher.PublishAsync(product.ToUpdatedEvent(), cancellationToken);

            return new UpdateProductResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product {ProductId}", request.ProductId);
            return new UpdateProductResult(false, ex.Message);
        }
    }
}
