using MediatR;
using Gearify.CatalogService.API.DTOs;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get a department by its slug along with its categories
/// </summary>
public record GetDepartmentBySlugQuery(string Slug) : IRequest<DepartmentWithCategoriesDto?>;
