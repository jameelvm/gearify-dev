using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;

namespace Gearify.SearchService.Infrastructure.OpenSearch;

public class IndexManager : IIndexManager
{
    private readonly IElasticClient _client;
    private readonly ILogger<IndexManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly HashSet<string> _ensuredIndexes = new();

    public IndexManager(
        IOpenSearchClientFactory clientFactory,
        ILogger<IndexManager> logger,
        IConfiguration configuration)
    {
        _client = clientFactory.CreateClient();
        _logger = logger;
        _configuration = configuration;
    }

    private bool IsLocalStack => _configuration.GetValue<bool>("LocalStack:UseLocalStack");

    public string GetIndexName(string tenantId, string indexType = IndexNames.Products)
    {
        return IndexNames.GetIndexName(tenantId, indexType);
    }

    public async Task EnsureProductIndexExistsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{tenantId}-{IndexNames.Products}";

        // Skip if we've already ensured this index exists in this session
        if (_ensuredIndexes.Contains(cacheKey))
            return;

        if (!await IndexExistsAsync(tenantId, IndexNames.Products, cancellationToken))
        {
            await CreateProductIndexAsync(tenantId, cancellationToken);
        }

        _ensuredIndexes.Add(cacheKey);
    }

    public async Task<bool> IndexExistsAsync(string tenantId, string indexType, CancellationToken cancellationToken = default)
    {
        var indexName = GetIndexName(tenantId, indexType);
        var response = await _client.Indices.ExistsAsync(indexName, ct: cancellationToken);
        return response.Exists;
    }

    public async Task<bool> CreateProductIndexAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (IsLocalStack)
        {
            return await CreateLocalStackIndexAsync(tenantId, cancellationToken);
        }
        return await CreateProductionIndexAsync(tenantId, cancellationToken);
    }

    private async Task<bool> CreateLocalStackIndexAsync(string tenantId, CancellationToken cancellationToken)
    {
        var indexName = GetIndexName(tenantId, IndexNames.Products);

        if (await IndexExistsAsync(tenantId, IndexNames.Products, cancellationToken))
        {
            _logger.LogInformation("Index {IndexName} already exists", indexName);
            return true;
        }

        _logger.LogInformation("Creating LocalStack product index {IndexName}", indexName);

        var createIndexResponse = await _client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .NumberOfShards(1)
                .NumberOfReplicas(0)
            )
            .Map<ProductSearchDocument>(m => m.Properties(ConfigureProductProperties)),
            cancellationToken
        );

        return HandleIndexCreationResponse(createIndexResponse, indexName);
    }

    private async Task<bool> CreateProductionIndexAsync(string tenantId, CancellationToken cancellationToken)
    {
        var indexName = GetIndexName(tenantId, IndexNames.Products);

        if (await IndexExistsAsync(tenantId, IndexNames.Products, cancellationToken))
        {
            _logger.LogInformation("Index {IndexName} already exists", indexName);
            return true;
        }

        _logger.LogInformation("Creating production product index {IndexName}", indexName);

        var createIndexResponse = await _client.Indices.CreateAsync(indexName, c => c
            .Settings(s => s
                .NumberOfShards(2)
                .NumberOfReplicas(1)
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("product_name_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase", "asciifolding", "edge_ngram_filter")
                        )
                        .Custom("autocomplete_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filters("lowercase", "asciifolding", "autocomplete_filter")
                        )
                    )
                    .TokenFilters(tf => tf
                        .EdgeNGram("edge_ngram_filter", e => e
                            .MinGram(2)
                            .MaxGram(20)
                        )
                        .EdgeNGram("autocomplete_filter", e => e
                            .MinGram(2)
                            .MaxGram(10)
                        )
                    )
                )
            )
            .Map<ProductSearchDocument>(m => m.Properties(ConfigureProductProperties)),
            cancellationToken
        );

        return HandleIndexCreationResponse(createIndexResponse, indexName);
    }

    private static IPromise<IProperties> ConfigureProductProperties(PropertiesDescriptor<ProductSearchDocument> p)
    {
        return p
            .Keyword(k => k.Name(n => n.Id))
            .Keyword(k => k.Name(n => n.TenantId))
            .Keyword(k => k.Name(n => n.Sku))
            .Text(t => t
                .Name(n => n.Name)
                .Fields(f => f
                    .Keyword(k => k.Name("keyword"))
                )
            )
            .Text(t => t
                .Name(n => n.Description)
                .Analyzer("standard")
            )
            .Text(t => t
                .Name(n => n.Brand)
                .Fields(f => f
                    .Keyword(k => k.Name("keyword"))
                )
            )
            .Keyword(k => k.Name(n => n.BrandSlug))
            .Keyword(k => k.Name(n => n.Department))
            .Keyword(k => k.Name(n => n.DepartmentSlug))
            .Keyword(k => k.Name(n => n.Category))
            .Keyword(k => k.Name(n => n.CategorySlug))
            .Keyword(k => k.Name(n => n.Subcategory))
            .Keyword(k => k.Name(n => n.SubcategorySlug))
            .Number(n => n.Name(nm => nm.Price).Type(NumberType.Double))
            .Number(n => n.Name(nm => nm.CompareAtPrice).Type(NumberType.Double))
            .Number(n => n.Name(nm => nm.DiscountPercentage).Type(NumberType.Double))
            .Keyword(k => k.Name(n => n.Currency))
            .Number(n => n.Name(nm => nm.RatingAverage).Type(NumberType.Double))
            .Number(n => n.Name(nm => nm.RatingCount).Type(NumberType.Integer))
            .Keyword(k => k.Name(n => n.ThumbnailUrl))
            .Keyword(k => k.Name(n => n.ImageUrls))
            .Keyword(k => k.Name(n => n.Tags))
            .Boolean(b => b.Name(n => n.IsActive))
            .Boolean(b => b.Name(n => n.IsDeal))
            .Boolean(b => b.Name(n => n.IsClearance))
            .Boolean(b => b.Name(n => n.IsNewArrival))
            .Boolean(b => b.Name(n => n.IsBestSeller))
            .Boolean(b => b.Name(n => n.IsFeatured))
            .Date(d => d.Name(n => n.CreatedAt))
            .Date(d => d.Name(n => n.UpdatedAt));
    }

    private bool HandleIndexCreationResponse(CreateIndexResponse response, string indexName)
    {
        if (!response.IsValid)
        {
            _logger.LogError("Failed to create index {IndexName}: {Error}",
                indexName, response.DebugInformation);
            return false;
        }

        _logger.LogInformation("Successfully created index {IndexName}", indexName);
        return true;
    }

    public async Task<bool> DeleteIndexAsync(string tenantId, string indexType, CancellationToken cancellationToken = default)
    {
        var indexName = GetIndexName(tenantId, indexType);

        if (!await IndexExistsAsync(tenantId, indexType, cancellationToken))
        {
            _logger.LogInformation("Index {IndexName} does not exist", indexName);
            return true;
        }

        _logger.LogInformation("Deleting index {IndexName}", indexName);

        var deleteResponse = await _client.Indices.DeleteAsync(indexName, ct: cancellationToken);

        if (!deleteResponse.IsValid)
        {
            _logger.LogError("Failed to delete index {IndexName}: {Error}",
                indexName, deleteResponse.DebugInformation);
            return false;
        }

        // Remove from cache
        _ensuredIndexes.Remove($"{tenantId}-{indexType}");

        _logger.LogInformation("Successfully deleted index {IndexName}", indexName);
        return true;
    }
}
