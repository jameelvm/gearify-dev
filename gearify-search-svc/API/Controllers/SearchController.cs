using Gearify.SearchService.Application.DTOs;
using Gearify.SearchService.Application.Queries;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.API.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IMediator mediator,
        ITenantContext tenantContext,
        ILogger<SearchController> logger)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Search products with full-text search, filters, and facets
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<SearchProductsResponse>> SearchProducts(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] string? brand,
        [FromQuery] string? department,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minRating,
        [FromQuery] string? tags,
        [FromQuery] bool? dealsOnly,
        [FromQuery] bool? clearanceOnly,
        [FromQuery] bool? newArrivalsOnly,
        [FromQuery] bool? bestSellersOnly,
        [FromQuery] string? sortBy = "relevance",
        [FromQuery] string? sortOrder = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? "default-tenant";

        _logger.LogInformation("Search request from tenant {TenantId}: q={Query}, category={Category}, brand={Brand}",
            tenantId, q, category, brand);

        var query = new SearchProductsQuery
        {
            TenantId = tenantId,
            SearchTerm = q,
            Category = category,
            Brand = brand,
            Department = department,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinRating = minRating,
            Tags = tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            DealsOnly = dealsOnly,
            ClearanceOnly = clearanceOnly,
            NewArrivalsOnly = newArrivalsOnly,
            BestSellersOnly = bestSellersOnly,
            SortBy = sortBy,
            SortDirection = sortOrder,
            Page = page,
            PageSize = Math.Min(pageSize, 100) // Cap at 100
        };

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Autocomplete suggestions as user types
    /// </summary>
    [HttpGet("autocomplete")]
    public async Task<ActionResult<AutocompleteResponse>> Autocomplete(
        [FromQuery] string? prefix,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? "default-tenant";

        _logger.LogInformation("Autocomplete request from tenant {TenantId}: prefix={Prefix}", tenantId, prefix);

        var query = new AutocompleteQuery
        {
            TenantId = tenantId,
            Prefix = prefix ?? string.Empty,
            Limit = Math.Min(limit, 20) // Cap at 20
        };

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get similar/recommended products for a given product
    /// Uses brand, category, price range, and tags for matching
    /// Future: Will support AI-based recommendations
    /// </summary>
    [HttpGet("similar/{productId}")]
    public async Task<ActionResult<SimilarProductsResponse>> GetSimilarProducts(
        [FromRoute] string productId,
        [FromQuery] int limit = 4,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? "default-tenant";

        _logger.LogInformation("Similar products request from tenant {TenantId} for product {ProductId}",
            tenantId, productId);

        var query = new SimilarProductsQuery
        {
            TenantId = tenantId,
            ProductId = productId,
            Limit = Math.Min(limit, 10) // Cap at 10
        };

        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }
}
