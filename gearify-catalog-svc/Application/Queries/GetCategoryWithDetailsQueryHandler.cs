using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetCategoryWithDetailsQueryHandler
    : IRequestHandler<GetCategoryWithDetailsQuery, CategoryWithDetailsDto?>
{
    private readonly ICategoryRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCategoryWithDetailsQueryHandler(ICategoryRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<CategoryWithDetailsDto?> Handle(GetCategoryWithDetailsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var (category, sections, subcategories) = await _repository.GetCategoryWithDetailsAsync(request.CategoryId, tenantId);

        // Return null if category not found or has invalid data
        if (category is null or { Id: null })
        {
            return null;
        }

        return CategoryWithDetailsDto.FromEntities(category, sections, subcategories);
    }
}
