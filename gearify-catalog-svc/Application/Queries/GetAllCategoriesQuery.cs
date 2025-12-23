using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetAllCategoriesQuery : IRequest<List<Category>>;
