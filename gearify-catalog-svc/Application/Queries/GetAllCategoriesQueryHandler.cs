using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetAllCategoriesQueryHandler : IRequestHandler<GetAllCategoriesQuery, List<CategoryDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetAllCategoriesQueryHandler(ICategoryRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<CategoryDto>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var categories = await _repository.GetAllCategoriesAsync(tenantId);

        return CategoryDto.FromEntities(categories);
    }
}
