using Gearify.CatalogService.Domain.Entities;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetCategoryWithDetailsQuery(string CategoryId)
    : IRequest<(Category category, List<CategorySection> sections, List<Subcategory> subcategories)>;
