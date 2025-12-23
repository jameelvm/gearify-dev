using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetAllCategoriesQuery : IRequest<List<CategoryDto>>;
