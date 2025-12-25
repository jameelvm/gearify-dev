using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Mappers;
using Gearify.CatalogService.Domain.Entities;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetMegaMenuDataQueryHandler
    : IRequestHandler<GetMegaMenuDataQuery, List<CategoryWithDetailsDto>>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISectionMapperFactory _sectionMapperFactory;
    private readonly ITenantContext _tenantContext;

    public GetMegaMenuDataQueryHandler(
        ICategoryRepository categoryRepository,
        ISectionMapperFactory sectionMapperFactory,
        ITenantContext tenantContext)
    {
        _categoryRepository = categoryRepository;
        _sectionMapperFactory = sectionMapperFactory;
        _tenantContext = tenantContext;
    }

    public async Task<List<CategoryWithDetailsDto>> Handle(GetMegaMenuDataQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Fetch all categories with details (sections and subcategories from DB)
        var categoriesWithDetails = await _categoryRepository.GetAllCategoriesWithDetailsAsync(tenantId);

        // Process each category to enrich subcategories via section mappings
        var enrichedCategories = new List<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>();

        foreach (var (category, sections, subcategories) in categoriesWithDetails)
        {
            // Process sections with mappings to enrich their subcategories
            foreach (var section in sections.Where(s => !string.IsNullOrEmpty(s.Mapping)))
            {
                var mapper = _sectionMapperFactory.GetMapper(section.Mapping!);
                if (mapper != null)
                {
                    // Get subcategories for this section
                    var sectionSubcategories = subcategories
                        .Where(sub => sub.SectionId == section.Id)
                        .ToList();

                    if (sectionSubcategories.Any())
                    {
                        // Enrich subcategories with data from the mapped source (e.g., brand details)
                        await mapper.EnrichAsync(sectionSubcategories, tenantId);
                    }
                }
            }

            enrichedCategories.Add((category, sections, subcategories));
        }

        // Transform to DTOs
        return enrichedCategories
            .Select(item => CategoryWithDetailsDto.FromEntities(
                item.category,
                item.sections,
                item.subcategories))
            .ToList();
    }
}
