using Gearify.CatalogService.API.DTOs;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetCategoryWithDetailsQuery(string CategoryId)
    : IRequest<CategoryWithDetailsDto?>;
