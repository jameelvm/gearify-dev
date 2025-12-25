using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get all active brands
/// </summary>
public class GetAllBrandsQuery : IRequest<List<BrandDto>>
{
}
