using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetCategoryWithDetailsQueryHandler
    : IRequestHandler<GetCategoryWithDetailsQuery, (Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
{
    private readonly ICategoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCategoryWithDetailsQueryHandler(ICategoryRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
        Handle(GetCategoryWithDetailsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        return await _repository.GetCategoryWithDetailsAsync(request.CategoryId, tenantId);
    }
}
