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
    /// Get all categories with their sections and items (complete mega menu data)
    /// Optimized to fetch all data in parallel for better performance
    /// </summary>
    [HttpGet("mega-menu")]
    [ProducesResponseType(typeof(List<CategoryWithDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMegaMenuData()
    {
        try
        {
            var result = await _mediator.Send(new GetMegaMenuDataQuery());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching mega menu data");
            return StatusCode(500, new { error = "Failed to fetch mega menu data" });
        }
    }
}
