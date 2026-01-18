using Gearify.AuthService.API.DTOs;
using Gearify.AuthService.Application.Commands;
using Gearify.AuthService.Application.Queries;
using Gearify.SharedKernel.Multitenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gearify.AuthService.API.Controllers;

/// <summary>
/// Controller for managing user delivery addresses
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AddressController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AddressController> _logger;

    public AddressController(IMediator mediator, ITenantContext tenantContext, ILogger<AddressController> logger)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all addresses for the current user
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAddresses()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = _tenantContext.TenantId;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var query = new GetUserAddressesQuery(userId, tenantId);
        var addresses = await _mediator.Send(query);

        return Ok(addresses);
    }

    /// <summary>
    /// Create a new address
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = _tenantContext.TenantId;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var command = new CreateAddressCommand(
            userId,
            tenantId,
            request.Label,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.ZipCode,
            request.Country,
            request.IsDefault
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Created($"/api/address/{result.AddressId}", new { id = result.AddressId });
    }

    /// <summary>
    /// Update an existing address
    /// </summary>
    [HttpPut("{addressId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAddress(string addressId, [FromBody] UpdateAddressRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = _tenantContext.TenantId;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var command = new UpdateAddressCommand(
            addressId,
            userId,
            tenantId,
            request.Label,
            request.FirstName,
            request.LastName,
            request.Phone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.ZipCode,
            request.Country,
            request.IsDefault
        );

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Address not found")
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }

    /// <summary>
    /// Delete an address
    /// </summary>
    [HttpDelete("{addressId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAddress(string addressId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = _tenantContext.TenantId;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var command = new DeleteAddressCommand(addressId, userId, tenantId);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Address not found")
            {
                return NotFound(new { error = result.ErrorMessage });
            }
            return BadRequest(new { error = result.ErrorMessage });
        }

        return NoContent();
    }
}
