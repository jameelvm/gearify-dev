using MediatR;
using Gearify.CatalogService.API.DTOs;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get all departments for the current tenant
/// </summary>
public record GetAllDepartmentsQuery : IRequest<List<DepartmentDto>>;
