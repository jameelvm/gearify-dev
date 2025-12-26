using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Clients;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.CatalogService.Application.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IProductRepository _repository;
    private readonly IMediaServiceClient _mediaServiceClient;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductByIdQueryHandler> _logger;

    public GetProductByIdQueryHandler(
        IProductRepository repository,
        IMediaServiceClient mediaServiceClient,
        ITenantContext tenantContext,
        ILogger<GetProductByIdQueryHandler> logger)
    {
        _repository = repository;
        _mediaServiceClient = mediaServiceClient;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var product = await _repository.GetByIdAsync(request.ProductId, tenantId);

        if (product == null)
        {
            return null;
        }

        // Fetch images from Media Service
        try
        {
            var images = await _mediaServiceClient.GetProductImagesAsync(request.ProductId, cancellationToken);
            return ProductDto.FromProduct(product, images);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to fetch images for product {ProductId}. Returning product without images.",
                request.ProductId);

            // Return product without images if Media Service call fails
            return ProductDto.FromProduct(product);
        }
    }
}

public class GetProductsByCategoryQueryHandler : IRequestHandler<GetProductsByCategoryQuery, List<Product>>
{
    private readonly IProductRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetProductsByCategoryQueryHandler(IProductRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<Product>> Handle(GetProductsByCategoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        return await _repository.GetByCategoryAsync(request.Category, tenantId);
    }
}

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, List<Product>>
{
    private readonly IProductRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAllProductsQueryHandler(IProductRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        return await _repository.GetAllAsync(tenantId, request.Skip, request.Take);
    }
}
