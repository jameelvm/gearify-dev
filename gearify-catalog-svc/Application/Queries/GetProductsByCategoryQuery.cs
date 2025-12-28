using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductsByCategoryQuery(string Category) : IRequest<List<ProductListDto>>;