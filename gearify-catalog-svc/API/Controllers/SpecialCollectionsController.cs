using Gearify.CatalogService.Application.DTOs;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog/special-collections")]
public class SpecialCollectionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SpecialCollectionsController> _logger;

    public SpecialCollectionsController(IMediator mediator, ILogger<SpecialCollectionsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get special product collections for a department (Deals, Clearance, etc.)
    /// Used to render special collection links in mega menu
    /// </summary>
    /// <param name="departmentSlug">Optional department slug to filter collections</param>
    [HttpGet]
    [ProducesResponseType(typeof(SpecialCollectionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSpecialCollections([FromQuery] string? departmentSlug = null)
    {
        try
        {
            var result = await _mediator.Send(new GetSpecialCollectionsQuery(departmentSlug));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching special collections for department {DepartmentSlug}", departmentSlug);
            return StatusCode(500, new { error = "Failed to fetch special collections" });
        }
    }
}
