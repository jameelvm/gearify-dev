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
        [FromQuery] string? category)
    {
        try
        {
            // If any slug parameters are provided, use slug-based query
            if (!string.IsNullOrEmpty(departmentSlug) || !string.IsNullOrEmpty(categorySlug) || !string.IsNullOrEmpty(subcategorySlug))
            {
                var result = await _mediator.Send(new GetProductsBySlugQuery(departmentSlug, categorySlug, subcategorySlug));
                return Ok(result);
            }

            // Legacy query param support
            var products = string.IsNullOrEmpty(category)
                ? await _mediator.Send(new GetAllProductsQuery())
                : await _mediator.Send(new GetProductsByCategoryQuery(category));

            return Ok(products);
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
}
