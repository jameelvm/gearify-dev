using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Mappers;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Configuration;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Handler for GetMegaMenuDataQuery
/// Fetches complete mega menu hierarchy: Departments → Categories → Sections → Subcategories
/// </summary>
public class GetMegaMenuDataQueryHandler : IRequestHandler<GetMegaMenuDataQuery, MegaMenuDto>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly Amazon.DynamoDBv2.IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ISectionMapperFactory _sectionMapperFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetMegaMenuDataQueryHandler> _logger;

    public GetMegaMenuDataQueryHandler(
        IDepartmentRepository departmentRepository,
        ICategoryRepository categoryRepository,
        Amazon.DynamoDBv2.IAmazonDynamoDB dynamoDb,
        IOptions<CatalogDataSettings> catalogDataSettings,
        ISectionMapperFactory sectionMapperFactory,
        ITenantContext tenantContext,
        ILogger<GetMegaMenuDataQueryHandler> logger)
    {
        _departmentRepository = departmentRepository;
        _dynamoDb = dynamoDb;
        _tableName = catalogDataSettings.Value.CatalogTableName;
        _sectionMapperFactory = sectionMapperFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<MegaMenuDto> Handle(GetMegaMenuDataQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        _logger.LogInformation("Fetching mega menu data for tenant: {TenantId}", tenantId);

        // Step 1: Get all departments
        var departments = await _departmentRepository.GetAllAsync(tenantId);
        _logger.LogInformation("Found {Count} departments for tenant {TenantId}", departments.Count, tenantId);

        var departmentMenus = new List<DepartmentMenuDto>();

        // Step 2: For each department, get its categories with details
        foreach (var department in departments.OrderBy(d => d.DisplayOrder))
        {
            _logger.LogDebug("Processing department: {DepartmentName} ({Slug})", department.Name, department.Slug);

            // Get all categories for this department
            var categories = await _departmentRepository.GetCategoriesAsync(department.Slug, tenantId);
            var categoryDetails = new List<CategoryWithDetailsDto>();

            // Step 3: For each category, get sections and subcategories
            foreach (var category in categories.OrderBy(c => c.DisplayOrder))
            {
                var (_, sections, subcategories) = await GetCategoryWithDetailsAsync(
                    category.Id,
                    category.DepartmentSlug,
                    tenantId
                );

                // Step 4: Enrich subcategories with mapped data (brands, etc.)
                await EnrichSubcategoriesAsync(sections, subcategories, tenantId);

                // Transform to DTO
                categoryDetails.Add(CategoryWithDetailsDto.FromEntities(category, sections, subcategories));
            }

            // Add department with its categories
            departmentMenus.Add(new DepartmentMenuDto
            {
                Id = department.Id,
                Name = department.Name,
                Slug = department.Slug,
                Icon = department.Icon,
                DisplayOrder = department.DisplayOrder,
                Categories = categoryDetails
            });
        }

        return new MegaMenuDto
        {
            Departments = departmentMenus
        };
    }

    /// <summary>
    /// Get category with sections and subcategories using the department-aware PK pattern
    /// </summary>
    private async Task<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>
        GetCategoryWithDetailsAsync(string categoryId, string departmentSlug, string tenantId)
    {
        var request = new Amazon.DynamoDBv2.Model.QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>
            {
                { ":pk", new Amazon.DynamoDBv2.Model.AttributeValue { S = $"TENANT#{tenantId}#DEPARTMENT#{departmentSlug}#CATEGORY#{categoryId}" } }
            }
        };

        var response = await _dynamoDb.QueryAsync(request);

        Category? category = null;
        var sections = new List<CategorySection>();
        var subcategories = new List<Subcategory>();

        foreach (var item in response.Items)
        {
            var sk = item["SK"].S;
            var entityType = item.TryGetValue("EntityType", out var et) ? et.S : string.Empty;

            if (sk == "METADATA" && entityType == "CATEGORY")
            {
                category = MapToCategory(item);
            }
            else if (entityType == "CATEGORY_SECTION")
            {
                sections.Add(MapToSection(item));
            }
            else if (entityType == "SUBCATEGORY")
            {
                subcategories.Add(MapToSubcategory(item));
            }
        }

        return (category ?? new Category(), sections.OrderBy(s => s.DisplayOrder).ToList(), subcategories.OrderBy(s => s.DisplayOrder).ToList());
    }

    /// <summary>
    /// Enrich subcategories with data from mapped sources (e.g., brand details)
    /// </summary>
    private async Task EnrichSubcategoriesAsync(
        List<CategorySection> sections,
        List<Subcategory> subcategories,
        string tenantId)
    {
        foreach (var section in sections.Where(s => !string.IsNullOrEmpty(s.Mapping)))
        {
            var mapper = _sectionMapperFactory.GetMapper(section.Mapping!);
            if (mapper != null)
            {
                var sectionSubcategories = subcategories
                    .Where(sub => sub.SectionId == section.Id)
                    .ToList();

                if (sectionSubcategories.Any())
                {
                    await mapper.EnrichAsync(sectionSubcategories, tenantId);
                }
            }
        }
    }

    #region Mapping Helpers

    private Category MapToCategory(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item)
    {
        return new Category
        {
            Id = item["Id"].S,
            TenantId = item["TenantId"].S,
            DepartmentId = item.TryGetValue("DepartmentId", out var deptId) ? deptId.S : string.Empty,
            DepartmentSlug = item.TryGetValue("DepartmentSlug", out var deptSlug) ? deptSlug.S : string.Empty,
            Name = item["Name"].S,
            Slug = item["Slug"].S,
            Description = item.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            Icon = item.TryGetValue("Icon", out var icon) ? icon.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var img) ? img.S : string.Empty,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var order) ? int.Parse(order.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };
    }

    private CategorySection MapToSection(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item)
    {

        if (item.TryGetValue("Mapping", out var mapping1))
        {
            var test = mapping1;
        }
        return new CategorySection
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            TenantId = item["TenantId"].S,
            Title = item["Title"].S,
            Slug = item.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
            ShowTitle = !item.TryGetValue("ShowTitle", out var show) || show.BOOL,
            Mapping = item.TryGetValue("Mapping", out var mapping) ? mapping.S : null,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var order) ? int.Parse(order.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };
    }

    private Subcategory MapToSubcategory(Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue> item)
    {
        return new Subcategory
        {
            Id = item["Id"].S,
            CategoryId = item["CategoryId"].S,
            SectionId = item["SectionId"].S,
            TenantId = item["TenantId"].S,
            Name = item["Name"].S,
            Slug = item.TryGetValue("Slug", out var slug) ? slug.S : string.Empty,
            Description = item.TryGetValue("Description", out var desc) ? desc.S : string.Empty,
            ImageUrl = item.TryGetValue("ImageUrl", out var img) ? img.S : string.Empty,
            BrandId = item.TryGetValue("BrandId", out var brandId) ? brandId.S : null,
            PriceRangeId = item.TryGetValue("PriceRangeId", out var priceRangeId) ? priceRangeId.S : null,
            FilterType = item.TryGetValue("FilterType", out var filterType) ? filterType.S : null,
            DisplayOrder = item.TryGetValue("DisplayOrder", out var order) ? int.Parse(order.N) : 0,
            ProductCount = item.TryGetValue("ProductCount", out var count) ? int.Parse(count.N) : 0,
            IsActive = item.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = DateTime.Parse(item["CreatedAt"].S),
            UpdatedAt = DateTime.Parse(item["UpdatedAt"].S)
        };
    }

    #endregion
}
