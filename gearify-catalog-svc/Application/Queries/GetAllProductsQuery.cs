using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetAllProductsQuery(int Skip = 0, int Take = 50) : IRequest<List<ProductListDto>>;