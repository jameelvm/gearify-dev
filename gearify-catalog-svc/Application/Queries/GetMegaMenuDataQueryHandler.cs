using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetMegaMenuDataQueryHandler
    : IRequestHandler<GetMegaMenuDataQuery, List<CategoryWithDetailsDto>>
{
    private readonly ICategoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetMegaMenuDataQueryHandler(ICategoryRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<List<CategoryWithDetailsDto>> Handle(GetMegaMenuDataQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Fetch all categories with details in parallel (optimized single call)
        var categoriesWithDetails = await _repository.GetAllCategoriesWithDetailsAsync(tenantId);

        // Transform to DTOs
        return categoriesWithDetails
            .Select(item => CategoryWithDetailsDto.FromEntities(item.category, item.sections, item.subcategories))
            .ToList();
    }
}
