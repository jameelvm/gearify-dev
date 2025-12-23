using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(IMediator mediator, ILogger<CategoriesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllCategories()
    {
        try
        {
            var response = await _mediator.Send(new GetAllCategoriesQuery());
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories");
            return StatusCode(500, new { error = "Failed to fetch categories" });
        }
    }

    /// <summary>
    /// Get category with all sections and subcategories (for mega menu)
    /// </summary>
    [HttpGet("{categoryId}/details")]
    [ProducesResponseType(typeof(CategoryWithDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryWithDetails(string categoryId)
    {
        try
        {
            var response = await _mediator.Send(new GetCategoryWithDetailsQuery(categoryId));

            if (response == null)
            {
                return NotFound(new { error = "Category not found" });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching category details for {CategoryId}", categoryId);
            return StatusCode(500, new { error = "Failed to fetch category details" });
        }
    }

    /// <summary>
    /// Get all categories with their sections and items (complete mega menu data)
    /// </summary>
    [HttpGet("mega-menu")]
    [ProducesResponseType(typeof(List<CategoryWithDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMegaMenuData()
    {
        try
        {
            var categories = await _mediator.Send(new GetAllCategoriesQuery());

            var result = new List<CategoryWithDetailsDto>();

            foreach (var category in categories)
            {
                var categoryDetails = await _mediator.Send(new GetCategoryWithDetailsQuery(category.Id));

                if (categoryDetails != null)
                {
                    result.Add(categoryDetails);
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching mega menu data");
            return StatusCode(500, new { error = "Failed to fetch mega menu data" });
        }
    }
}
