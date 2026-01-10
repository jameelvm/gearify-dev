using Gearify.SearchService.Application.Services;
using Gearify.SearchService.Domain.Entities;
using Gearify.SearchService.Infrastructure.Configuration;
using Gearify.SearchService.Infrastructure.OpenSearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.SearchService.API.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IIndexManager _indexManager;
    private readonly IProductIndexService _productIndexService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IIndexManager indexManager,
        IProductIndexService productIndexService,
        ILogger<AdminController> logger)
    {
        _indexManager = indexManager;
        _productIndexService = productIndexService;
        _logger = logger;
    }

    /// <summary>
    /// Create product search index for a tenant
    /// </summary>
    [HttpPost("index/{tenantId}/products")]
    public async Task<ActionResult> CreateProductIndex(string tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating product index for tenant: {TenantId}", tenantId);

        var success = await _indexManager.CreateProductIndexAsync(tenantId, cancellationToken);

        if (!success)
        {
            return StatusCode(500, new { error = "Failed to create product index" });
        }

        return Ok(new
        {
            message = "Product index created successfully",
            indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products)
        });
    }

    /// <summary>
    /// Check if product index exists for a tenant
    /// </summary>
    [HttpGet("index/{tenantId}/products/exists")]
    public async Task<ActionResult> ProductIndexExists(string tenantId, CancellationToken cancellationToken)
    {
        var exists = await _indexManager.IndexExistsAsync(tenantId, IndexNames.Products, cancellationToken);

        return Ok(new
        {
            tenantId,
            indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products),
            exists
        });
    }

    /// <summary>
    /// Delete product search index for a tenant
    /// </summary>
    [HttpDelete("index/{tenantId}/products")]
    public async Task<ActionResult> DeleteProductIndex(string tenantId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting product index for tenant: {TenantId}", tenantId);

        var success = await _indexManager.DeleteIndexAsync(tenantId, IndexNames.Products, cancellationToken);

        if (!success)
        {
            return StatusCode(500, new { error = "Failed to delete product index" });
        }

        return Ok(new
        {
            message = "Product index deleted successfully",
            indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products)
        });
    }

    /// <summary>
    /// Index a test product (for testing)
    /// </summary>
    [HttpPost("products/test")]
    public async Task<ActionResult> IndexTestProduct(
        [FromQuery] string tenantId = "default-tenant",
        CancellationToken cancellationToken = default)
    {
        var testProduct = new ProductSearchDocument
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Sku = "TEST-SKU-001",
            Name = "Nike Air Zoom Pegasus 40",
            Description = "The Nike Air Zoom Pegasus 40 is a running shoe with responsive cushioning.",
            Brand = "Nike",
            BrandSlug = "nike",
            Department = "Footwear",
            DepartmentSlug = "footwear",
            Category = "Running Shoes",
            CategorySlug = "running-shoes",
            Price = 129.99m,
            CompareAtPrice = 149.99m,
            DiscountPercentage = 13.33m,
            Currency = "USD",
            RatingAverage = 4.5m,
            RatingCount = 1250,
            Tags = new List<string> { "running", "athletic", "comfortable" },
            IsActive = true,
            IsDeal = true,
            IsNewArrival = false,
            IsBestSeller = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var success = await _productIndexService.IndexProductAsync(testProduct, cancellationToken);

        if (!success)
        {
            return StatusCode(500, new { error = "Failed to index test product" });
        }

        return Ok(new
        {
            message = "Test product indexed successfully",
            productId = testProduct.Id,
            indexName = _indexManager.GetIndexName(tenantId, IndexNames.Products)
        });
    }

    /// <summary>
    /// Delete a product by ID
    /// </summary>
    [HttpDelete("products/{productId}")]
    public async Task<ActionResult> DeleteProduct(
        string productId,
        [FromQuery] string tenantId = "default-tenant",
        CancellationToken cancellationToken = default)
    {
        var success = await _productIndexService.DeleteProductAsync(productId, tenantId, cancellationToken);

        if (!success)
        {
            return StatusCode(500, new { error = "Failed to delete product" });
        }

        return Ok(new
        {
            message = "Product deleted successfully",
            productId
        });
    }
}
