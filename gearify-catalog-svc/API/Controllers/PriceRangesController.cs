using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog/price-ranges")]
public class PriceRangesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PriceRangesController> _logger;

    public PriceRangesController(IMediator mediator, ILogger<PriceRangesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all price ranges with product counts
    /// </summary>
    /// <param name="category">Optional category to filter price ranges</param>
    /// <param name="onlyCategorySpecific">If true, only return category-specific ranges (exclude global)</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<PriceRangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPriceRanges(
        [FromQuery] string? category = null,
        [FromQuery] bool onlyCategorySpecific = false)
    {
        try
        {
            var result = await _mediator.Send(new GetPriceRangesQuery
            {
                Category = category,
                OnlyCategorySpecific = onlyCategorySpecific
            });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price ranges");
            return StatusCode(500, new { error = "Failed to fetch price ranges" });
        }
    }
}
