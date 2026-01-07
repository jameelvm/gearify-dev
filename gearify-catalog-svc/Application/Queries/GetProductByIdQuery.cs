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
    string? SortBy = null,  // Sorting parameter
    string? CollectionId = null,     // Special collection filter (deals, clearance, new-arrivals, etc.)
    int PageSize = 12,               // Items per page
    string? Cursor = null            // Pagination cursor (base64 encoded LastEvaluatedKey)
) : IRequest<ProductListResponse>;

public record ProductListResponse(
    List<ProductListDto> Products,
    int Total,
    bool HasMore,
    string? NextCursor);

public record GetSpecialCollectionsQuery(string? DepartmentSlug = null) : IRequest<SpecialCollectionsResponse>;

public record SpecialCollectionsResponse(List<SpecialCollectionDto> Collections);
