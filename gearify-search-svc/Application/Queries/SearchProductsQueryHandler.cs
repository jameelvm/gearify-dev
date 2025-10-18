using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.SearchService.Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.Application.Queries;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResult>
{
    private readonly ISearchRepository _repository;
    private readonly ILogger<SearchProductsQueryHandler> _logger;

    public SearchProductsQueryHandler(ISearchRepository repository, ILogger<SearchProductsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<SearchProductsResult> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching products for tenant {TenantId} with term: {SearchTerm}",
            request.TenantId, request.SearchTerm ?? "all");

        var results = await _repository.SearchAsync(
            request.TenantId,
            request.SearchTerm,
            request.Category,
            request.MinPrice,
            request.MaxPrice,
            request.Brand
        );

        return new SearchProductsResult(results, results.Count);
    }
}
