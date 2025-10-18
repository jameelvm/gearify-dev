using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.SearchService.Application.Queries;

namespace Gearify.SearchService.Infrastructure.Repositories;

public interface ISearchRepository
{
    Task<List<ProductSearchResult>> SearchAsync(
        string tenantId,
        string? searchTerm,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        string? brand
    );
}
