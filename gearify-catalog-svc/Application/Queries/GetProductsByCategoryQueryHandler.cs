using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetProductsByCategoryQueryHandler : IRequestHandler<GetProductsByCategoryQuery, List<ProductListDto>>
{
    private readonly IProductRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetProductsByCategoryQueryHandler(IProductRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<ProductListDto>> Handle(GetProductsByCategoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var products = await _repository.GetByCategoryAsync(request.Category, tenantId);
        return products.Select(ProductListDto.FromProduct).ToList();
    }
}