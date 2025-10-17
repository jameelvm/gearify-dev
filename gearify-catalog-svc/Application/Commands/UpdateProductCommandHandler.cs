using Gearify.CatalogService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<UpdateProductCommandHandler> _logger;

    public UpdateProductCommandHandler(
        IProductRepository repository,
        ILogger<UpdateProductCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<UpdateProductResult> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _repository.GetByIdAsync(request.ProductId, request.TenantId);
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

            return new UpdateProductResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update product {ProductId}", request.ProductId);
            return new UpdateProductResult(false, ex.Message);
        }
    }
}
