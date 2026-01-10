using Gearify.SearchService.Application.DTOs;
using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Gearify.SearchService.Infrastructure.OpenSearch;
using MediatR;
using Microsoft.Extensions.Logging;
using Nest;

namespace Gearify.SearchService.Application.Queries;

public class AutocompleteQueryHandler : IRequestHandler<AutocompleteQuery, AutocompleteResponse>
{
    private readonly IElasticClient _client;
    private readonly IIndexManager _indexManager;
    private readonly ILogger<AutocompleteQueryHandler> _logger;

    public AutocompleteQueryHandler(
        IOpenSearchClientFactory clientFactory,
        IIndexManager indexManager,
        ILogger<AutocompleteQueryHandler> logger)
    {
        _client = clientFactory.CreateClient();
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task<AutocompleteResponse> Handle(AutocompleteQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId ?? "default-tenant";
        var indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products);

        if (string.IsNullOrWhiteSpace(request.Prefix) || request.Prefix.Length < 2)
        {
            return new AutocompleteResponse();
        }

        _logger.LogDebug("Autocomplete search in index {IndexName} with prefix: {Prefix}", indexName, request.Prefix);

        var suggestions = new List<AutocompleteSuggestion>();

        // Search for product name matches
        var productSuggestions = await GetProductSuggestionsAsync(indexName, request.Prefix, request.Limit, cancellationToken);
        suggestions.AddRange(productSuggestions);

        // Search for brand matches
        var brandSuggestions = await GetBrandSuggestionsAsync(indexName, request.Prefix, 5, cancellationToken);
        suggestions.AddRange(brandSuggestions);

        // Deduplicate and limit results
        var result = suggestions
            .DistinctBy(s => s.Text.ToLowerInvariant())
            .Take(request.Limit)
            .ToList();

        return new AutocompleteResponse
        {
            Suggestions = result
        };
    }

    private async Task<List<AutocompleteSuggestion>> GetProductSuggestionsAsync(
        string indexName, string prefix, int limit, CancellationToken cancellationToken)
    {
        var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
            .Index(indexName)
            .Size(limit)
            .Source(src => src.Includes(i => i.Fields(
                f => f.Id,
                f => f.Name,
                f => f.Brand,
                f => f.Category
            )))
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.Term(t => t.Field(f => f.IsActive).Value(true)),
                        m => m.MultiMatch(mm => mm
                            .Fields(f => f
                                .Field("name.autocomplete", boost: 3)
                                .Field("brand.autocomplete", boost: 2)
                            )
                            .Query(prefix)
                            .Type(TextQueryType.BoolPrefix)
                        )
                    )
                )
            )
            .Highlight(h => h
                .Fields(
                    f => f.Field("name.autocomplete").NumberOfFragments(0),
                    f => f.Field("brand.autocomplete").NumberOfFragments(0)
                )
                .PreTags("")
                .PostTags("")
            ),
            cancellationToken
        );

        if (!searchResponse.IsValid)
        {
            _logger.LogWarning("Autocomplete search failed: {Error}", searchResponse.DebugInformation);
            return new List<AutocompleteSuggestion>();
        }

        return searchResponse.Documents.Select(doc => new AutocompleteSuggestion
        {
            Text = doc.Name,
            Type = "product",
            Id = doc.Id
        }).ToList();
    }

    private async Task<List<AutocompleteSuggestion>> GetBrandSuggestionsAsync(
        string indexName, string prefix, int limit, CancellationToken cancellationToken)
    {
        var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
            .Index(indexName)
            .Size(0)
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.Term(t => t.Field(f => f.IsActive).Value(true)),
                        m => m.Match(mm => mm
                            .Field("brand.autocomplete")
                            .Query(prefix)
                        )
                    )
                )
            )
            .Aggregations(a => a
                .Terms("brands", t => t
                    .Field("brand.keyword")
                    .Size(limit)
                )
            ),
            cancellationToken
        );

        if (!searchResponse.IsValid)
        {
            _logger.LogWarning("Brand autocomplete search failed: {Error}", searchResponse.DebugInformation);
            return new List<AutocompleteSuggestion>();
        }

        var brandsBucket = searchResponse.Aggregations.Terms("brands");
        if (brandsBucket == null)
        {
            return new List<AutocompleteSuggestion>();
        }

        return brandsBucket.Buckets
            .Where(b => !string.IsNullOrWhiteSpace(b.Key) &&
                        b.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(b => new AutocompleteSuggestion
            {
                Text = b.Key,
                Type = "brand",
                Slug = b.Key.ToLowerInvariant().Replace(" ", "-")
            })
            .ToList();
    }
}
