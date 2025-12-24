using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetMegaMenuDataQuery : IRequest<List<CategoryWithDetailsDto>>;
