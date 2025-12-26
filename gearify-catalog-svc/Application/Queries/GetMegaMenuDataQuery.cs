using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get complete mega menu data with department hierarchy
/// Returns all departments with their categories, sections, and subcategories
/// </summary>
public record GetMegaMenuDataQuery : IRequest<MegaMenuDto>;
