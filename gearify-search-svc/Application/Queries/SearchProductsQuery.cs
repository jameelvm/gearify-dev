using System.Collections.Generic;
using MediatR;

namespace Gearify.SearchService.Application.Queries;

public record SearchProductsQuery(
    string TenantId,
    string? SearchTerm = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null
) : IRequest<SearchProductsResult>;

public record SearchProductsResult(List<ProductSearchResult> Products, int TotalCount);

public record ProductSearchResult(
    string Id,
    string Name,
    string Category,
    decimal Price,
    string Brand,
    string ImageUrl
);
