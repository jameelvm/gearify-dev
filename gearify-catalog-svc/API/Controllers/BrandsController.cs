using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog/brands")]
public class BrandsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BrandsController> _logger;

    public BrandsController(IMediator mediator, ILogger<BrandsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all brands with product counts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<BrandDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllBrands()
    {
        try
        {
            var result = await _mediator.Send(new GetAllBrandsQuery());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching brands");
            return StatusCode(500, new { error = "Failed to fetch brands" });
        }
    }
}
