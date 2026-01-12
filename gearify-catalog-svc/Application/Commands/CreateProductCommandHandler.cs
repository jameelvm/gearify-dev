using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Domain.Events;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ISnsEventPublisher _eventPublisher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ISnsEventPublisher eventPublisher,
        ITenantContext tenantContext,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _tenantContext.TenantId;

            var product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Sku = request.Sku,
                Name = request.Name,
                Description = request.Description,
                Category = request.Category,
                Brand = request.Brand,
                Price = request.Price,
                CompareAtPrice = request.CompareAtPrice,
                Tags = request.Tags,
                Attributes = request.Attributes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.CreateAsync(product);

            _logger.LogInformation("Product created: {ProductId} for tenant {TenantId}", product.Id, product.TenantId);

            // Publish event for Search Service to index the product
            await _eventPublisher.PublishAsync(product.ToCreatedEvent(), cancellationToken);

            return new CreateProductResult(product.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product for tenant {TenantId}", _tenantContext.TenantId);
            return new CreateProductResult(string.Empty, false, ex.Message);
        }
    }
}
