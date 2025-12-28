using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, List<ProductListDto>>
{
    private readonly IProductRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAllProductsQueryHandler(IProductRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<ProductListDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var products = await _repository.GetAllAsync(tenantId, request.Skip, request.Take);
        return products.Select(ProductListDto.FromProduct).ToList();
    }
}