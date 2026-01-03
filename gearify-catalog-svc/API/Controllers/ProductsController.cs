using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IMediator mediator, ILogger<ProductsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? departmentSlug,
        [FromQuery] string? categorySlug,
        [FromQuery] string? subcategorySlug,
        [FromQuery] string? brandSlug,
        [FromQuery] string? category,
        [FromQuery] string? priceRange)
    {
        try
        {
            // Parse price range if provided (format: "min-max" e.g., "0-5000")
            decimal? minPrice = null;
            decimal? maxPrice = null;
            if (!string.IsNullOrEmpty(priceRange))
            {
                var parts = priceRange.Split('-');
                if (parts.Length == 2)
                {
                    if (decimal.TryParse(parts[0], out var min))
                        minPrice = min;
                    if (decimal.TryParse(parts[1], out var max))
                        maxPrice = max;
                }
            }

            // If any slug parameters are provided, use slug-based query
            if (!string.IsNullOrEmpty(departmentSlug) || !string.IsNullOrEmpty(categorySlug) || !string.IsNullOrEmpty(subcategorySlug) || !string.IsNullOrEmpty(brandSlug) || minPrice.HasValue || maxPrice.HasValue)
            {
                var result = await _mediator.Send(new GetProductsBySlugQuery(departmentSlug, categorySlug, subcategorySlug, brandSlug, minPrice, maxPrice));
                return Ok(result);
            }

            // Legacy query param support - wrap in ProductListResponse
            var products = string.IsNullOrEmpty(category)
                ? await _mediator.Send(new GetAllProductsQuery())
                : await _mediator.Send(new GetProductsByCategoryQuery(category));

            var response = new ProductListResponse(products, products.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProduct(string id)
    {
        try
        {
            var product = await _mediator.Send(new GetProductByIdQuery(id));
            return product == null ? NotFound() : Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return CreatedAtAction(nameof(GetProduct), new { id = result.ProductId }, new { id = result.ProductId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] UpdateProductCommand command)
    {
        try
        {
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product {ProductId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Upload images for a product
    /// </summary>
    [HttpPost("products/{id}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UploadProductImages(
        string id,
        [FromForm] List<IFormFile>? images,
        [FromForm] List<string>? altTexts = null)
    {
        try
        {
            if (images == null || !images.Any())
            {
                return BadRequest(new { error = "No images provided" });
            }

            var command = new UploadProductImagesCommand(
                ProductId: id,
                Images: images,
                AltTexts: altTexts);

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new
            {
                success = true,
                uploadedCount = result.UploadedImages?.Count ?? 0,
                images = result.UploadedImages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading images for product {ProductId}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
