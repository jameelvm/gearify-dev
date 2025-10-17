using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductByIdQuery(string ProductId, string TenantId) : IRequest<Product?>;

public record GetProductsByCategoryQuery(string Category, string TenantId) : IRequest<List<Product>>;

public record GetAllProductsQuery(string TenantId, int Skip = 0, int Take = 50) : IRequest<List<Product>>;
