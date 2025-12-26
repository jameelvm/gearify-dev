using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductByIdQuery(string ProductId) : IRequest<ProductDto?>;

public record GetProductsByCategoryQuery(string Category) : IRequest<List<Product>>;

public record GetAllProductsQuery(int Skip = 0, int Take = 50) : IRequest<List<Product>>;

public record GetProductsBySlugQuery(
    string? DepartmentSlug,
    string? CategorySlug,
    string? SubcategorySlug
) : IRequest<ProductListResponse>;

public record ProductListResponse(List<Product> Products, int Total);
