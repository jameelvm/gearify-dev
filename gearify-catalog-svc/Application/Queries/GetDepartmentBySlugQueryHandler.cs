using MediatR;
using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for GetDepartmentBySlugQuery
/// Returns department details with all its categories
/// </summary>
public class GetDepartmentBySlugQueryHandler : IRequestHandler<GetDepartmentBySlugQuery, DepartmentWithCategoriesDto?>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetDepartmentBySlugQueryHandler> _logger;

    public GetDepartmentBySlugQueryHandler(
        IDepartmentRepository departmentRepository,
        ITenantContext tenantContext,
        ILogger<GetDepartmentBySlugQueryHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DepartmentWithCategoriesDto?> Handle(GetDepartmentBySlugQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        _logger.LogInformation("Fetching department {Slug} for tenant: {TenantId}", request.Slug, tenantId);

        var department = await _departmentRepository.GetBySlugAsync(request.Slug, tenantId);
        if (department == null)
        {
            _logger.LogWarning("Department {Slug} not found for tenant: {TenantId}", request.Slug, tenantId);
            return null;
        }

        var categories = await _departmentRepository.GetCategoriesAsync(department.Slug, tenantId);

        return new DepartmentWithCategoriesDto
        {
            Id = department.Id,
            Name = department.Name,
            Slug = department.Slug,
            Description = department.Description,
            Icon = department.Icon,
            ImageUrl = department.ImageUrl,
            DisplayOrder = department.DisplayOrder,
            Categories = categories.Select(c => new CategorySummaryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                Icon = c.Icon,
                ImageUrl = c.ImageUrl,
                DisplayOrder = c.DisplayOrder
            }).OrderBy(c => c.DisplayOrder).ToList()
        };
    }
}
