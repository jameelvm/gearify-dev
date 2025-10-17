using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = request.TenantId,
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

            return new CreateProductResult(product.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create product for tenant {TenantId}", request.TenantId);
            return new CreateProductResult(string.Empty, false, ex.Message);
        }
    }
}
