using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _repository;

    public GetProductByIdQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<Product?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByIdAsync(request.ProductId, request.TenantId);
    }
}

public class GetProductsByCategoryQueryHandler : IRequestHandler<GetProductsByCategoryQuery, List<Product>>
{
    private readonly IProductRepository _repository;

    public GetProductsByCategoryQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Product>> Handle(GetProductsByCategoryQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByCategoryAsync(request.Category, request.TenantId);
    }
}

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, List<Product>>
{
    private readonly IProductRepository _repository;

    public GetAllProductsQueryHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<Product>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetAllAsync(request.TenantId, request.Skip, request.Take);
    }
}
