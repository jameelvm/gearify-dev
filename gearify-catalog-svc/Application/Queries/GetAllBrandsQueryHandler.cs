using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetAllBrandsQueryHandler : IRequestHandler<GetAllBrandsQuery, List<BrandDto>>
{
    private readonly IBrandRepository _brandRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public GetAllBrandsQueryHandler(
        IBrandRepository brandRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _brandRepository = brandRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<List<BrandDto>> Handle(GetAllBrandsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Get all brands
        var brands = await _brandRepository.GetAllBrandsAsync(tenantId);

        // Get product counts for each brand in parallel
        var brandDtos = new List<BrandDto>();

        foreach (var brand in brands)
        {
            var productCount = await _productRepository.GetProductCountByBrandAsync(brand.Id, tenantId);

            brandDtos.Add(new BrandDto
            {
                Id = brand.Id,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                Logo = brand.Logo,
                ProductCount = productCount
            });
        }

        return brandDtos.OrderBy(b => b.Name).ToList();
    }
}
