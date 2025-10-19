using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gearify.SearchService.Infrastructure.Repositories;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.Application.Queries;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchProductsResult>
{
    private readonly ISearchRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SearchProductsQueryHandler> _logger;

    public SearchProductsQueryHandler(
        ISearchRepository repository,
        ITenantContext tenantContext,
        ILogger<SearchProductsQueryHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<SearchProductsResult> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;

        _logger.LogInformation("Searching products for tenant {TenantId} with term: {SearchTerm}",
            tenantId, request.SearchTerm ?? "all");

        var results = await _repository.SearchAsync(
            tenantId,
            request.SearchTerm,
            request.Category,
            request.MinPrice,
            request.MaxPrice,
            request.Brand
        );

        return new SearchProductsResult(results, results.Count);
    }
}
