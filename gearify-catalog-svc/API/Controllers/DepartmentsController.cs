using Gearify.CatalogService.API.DTOs;
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API.Controllers;

[ApiController]
[Route("api/catalog/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DepartmentsController> _logger;

    public DepartmentsController(IMediator mediator, ILogger<DepartmentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all departments for the current tenant
    /// Returns department list with category counts
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllDepartments()
    {
        try
        {
            var result = await _mediator.Send(new GetAllDepartmentsQuery());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching departments");
            return StatusCode(500, new { error = "Failed to fetch departments" });
        }
    }

    /// <summary>
    /// Get a specific department by slug with its categories
    /// </summary>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(DepartmentWithCategoriesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepartmentBySlug(string slug)
    {
        try
        {
            var result = await _mediator.Send(new GetDepartmentBySlugQuery(slug));

            if (result == null)
            {
                return NotFound(new { error = $"Department '{slug}' not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching department {Slug}", slug);
            return StatusCode(500, new { error = "Failed to fetch department" });
        }
    }

    /// <summary>
    /// Get categories for a specific department
    /// </summary>
    [HttpGet("{slug}/categories")]
    [ProducesResponseType(typeof(List<CategorySummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepartmentCategories(string slug)
    {
        try
        {
            var result = await _mediator.Send(new GetDepartmentBySlugQuery(slug));

            if (result == null)
            {
                return NotFound(new { error = $"Department '{slug}' not found" });
            }

            return Ok(result.Categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching categories for department {Slug}", slug);
            return StatusCode(500, new { error = "Failed to fetch categories" });
        }
    }
}
