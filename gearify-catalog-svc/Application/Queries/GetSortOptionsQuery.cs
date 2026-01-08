using Gearify.CatalogService.Application.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

/// <summary>
/// Query to get sort options for a tenant
/// Sort options are stored at tenant level (not department-specific)
/// </summary>
public record GetSortOptionsQuery() : IRequest<SortOptionsResponse>;
