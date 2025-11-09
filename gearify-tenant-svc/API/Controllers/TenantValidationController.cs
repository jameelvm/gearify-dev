using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gearify.TenantService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.TenantService.API.Controllers;

/// <summary>
/// Public endpoint for validating tenant existence (no auth required)
/// </summary>
[ApiController]
[Route("api/tenants")]
public class TenantValidationController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantValidationController> _logger;

    public TenantValidationController(
        ITenantRepository tenantRepository,
        ILogger<TenantValidationController> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Validates if a tenant exists and is active
    /// This is a public endpoint that doesn't require authentication
    /// </summary>
    /// <param name="tenantId">The tenant ID to validate</param>
    /// <returns>200 if valid, 404 if not found</returns>
    [HttpGet("validate/{tenantId}")]
    [ProducesResponseType(typeof(TenantValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return NotFound(new { error = "Tenant ID is required" });
        }

        var isValid = await _tenantRepository.ExistsAndActiveAsync(tenantId);

        if (!isValid)
        {
            _logger.LogWarning("Tenant validation failed for: {TenantId}", tenantId);
            return NotFound(new
            {
                error = "Tenant not found",
                message = $"The tenant '{tenantId}' does not exist or is not active.",
                tenantId
            });
        }

        _logger.LogInformation("Tenant validated successfully: {TenantId}", tenantId);
        return Ok(new TenantValidationResponse
        {
            TenantId = tenantId,
            IsValid = true,
            IsActive = true
        });
    }

    /// <summary>
    /// Gets list of all active tenants (for development/testing)
    /// TODO: Remove or secure this endpoint in production
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTenants()
    {
        var tenants = await _tenantRepository.GetAllActiveAsync();
        var tenantIds = tenants.Select(t => t.Id).OrderBy(id => id);
        return Ok(tenantIds);
    }
}

public record TenantValidationResponse
{
    public string TenantId { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public bool IsActive { get; init; }
}
