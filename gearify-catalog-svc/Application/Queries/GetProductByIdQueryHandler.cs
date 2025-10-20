using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetProductByIdQueryHandler(IProductRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Product?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        return await _repository.GetByIdAsync(request.ProductId, tenantId);
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
