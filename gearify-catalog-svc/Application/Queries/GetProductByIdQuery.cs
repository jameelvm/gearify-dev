using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductByIdQuery(string ProductId) : IRequest<ProductDto?>;

public record GetProductsBySlugQuery(
    string? DepartmentSlug,
    string? CategorySlug,
    string? SubcategorySlug,
    string[]? BrandSlugs,  // Changed from single BrandSlug to array
    decimal? MinPrice,
    decimal? MaxPrice,
    string? SortBy = null  // New parameter for sorting
) : IRequest<ProductListResponse>;

public record ProductListResponse(List<ProductListDto> Products, int Total);
