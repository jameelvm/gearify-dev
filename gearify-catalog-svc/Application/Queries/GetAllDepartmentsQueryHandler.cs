using MediatR;
using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for GetAllDepartmentsQuery
/// Returns all departments for the current tenant with category counts
/// </summary>
public class GetAllDepartmentsQueryHandler : IRequestHandler<GetAllDepartmentsQuery, List<DepartmentDto>>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAllDepartmentsQueryHandler> _logger;

    public GetAllDepartmentsQueryHandler(
        IDepartmentRepository departmentRepository,
        ITenantContext tenantContext,
        ILogger<GetAllDepartmentsQueryHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<DepartmentDto>> Handle(GetAllDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        _logger.LogInformation("Fetching all departments for tenant: {TenantId}", tenantId);

        var departments = await _departmentRepository.GetAllAsync(tenantId);

        var departmentDtos = new List<DepartmentDto>();

        foreach (var department in departments)
        {
            // Get category count for each department
            var categories = await _departmentRepository.GetCategoriesAsync(department.Slug, tenantId);

            departmentDtos.Add(new DepartmentDto
            {
                Id = department.Id,
                Name = department.Name,
                Slug = department.Slug,
                Description = department.Description,
                Icon = department.Icon,
                ImageUrl = department.ImageUrl,
                DisplayOrder = department.DisplayOrder,
                CategoryCount = categories.Count
            });
        }

        return departmentDtos.OrderBy(d => d.DisplayOrder).ToList();
    }
}
