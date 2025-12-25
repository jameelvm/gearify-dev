using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public class GetPriceRangesQueryHandler : IRequestHandler<GetPriceRangesQuery, List<PriceRangeDto>>
{
    private readonly IPriceRangeRepository _priceRangeRepository;
    private readonly IProductRepository _productRepository;
    private readonly ITenantContext _tenantContext;

    public GetPriceRangesQueryHandler(
        IPriceRangeRepository priceRangeRepository,
        IProductRepository productRepository,
        ITenantContext tenantContext)
    {
        _priceRangeRepository = priceRangeRepository;
        _productRepository = productRepository;
        _tenantContext = tenantContext;
    }

    public async Task<List<PriceRangeDto>> Handle(GetPriceRangesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        // Get all price ranges for the tenant
        var priceRanges = await _priceRangeRepository.GetPriceRangesAsync(
            tenantId,
            request.Category,
            request.OnlyCategorySpecific);

        // Get all products to calculate counts
        var products = await _productRepository.GetAllAsync(tenantId, skip: 0, take: 10000);

        // Map to DTOs and calculate product counts
        var priceRangeDtos = new List<PriceRangeDto>();

        foreach (var range in priceRanges)
        {
            // Count products in this price range
            var productCount = products.Count(p =>
                p.Price >= range.MinPrice &&
                (range.MaxPrice == null || p.Price <= range.MaxPrice));

            // Generate value string for filtering
            var value = range.MaxPrice.HasValue
                ? $"{range.MinPrice}-{range.MaxPrice}"
                : $"{range.MinPrice}+";

            priceRangeDtos.Add(new PriceRangeDto
            {
                Id = range.Id,
                Label = range.Label,
                MinPrice = range.MinPrice,
                MaxPrice = range.MaxPrice,
                Currency = range.Currency,
                DisplayOrder = range.DisplayOrder,
                Category = range.Category,
                ProductCount = productCount,
                Value = value
            });
        }

        return priceRangeDtos.OrderBy(pr => pr.DisplayOrder).ToList();
    }
}
