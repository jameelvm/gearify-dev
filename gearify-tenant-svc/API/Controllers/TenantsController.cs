using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gearify.TenantService.Domain.Entities;
using Gearify.TenantService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Gearify.TenantService.API.Controllers;

/// <summary>
/// Tenant management endpoints (admin only)
/// </summary>
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantRepository tenantRepository,
        ILogger<TenantsController> logger)
    {
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all tenants
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Tenant>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var tenants = activeOnly
            ? await _tenantRepository.GetAllActiveAsync()
            : await _tenantRepository.GetAllAsync();

        return Ok(tenants);
    }

    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    [HttpGet("{tenantId}")]
    [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string tenantId)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            return NotFound(new { error = "Tenant not found", tenantId });
        }

        return Ok(tenant);
    }

    /// <summary>
    /// Creates a new tenant
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Tenant), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        // Validate tenant ID format (slug)
        if (!IsValidSlug(request.Id))
        {
            return BadRequest(new
            {
                error = "Invalid tenant ID",
                message = "Tenant ID must be lowercase alphanumeric with hyphens only (e.g., 'acme-corp')"
            });
        }

        // Check if tenant already exists
        var existing = await _tenantRepository.GetByIdAsync(request.Id);
        if (existing != null)
        {
            return BadRequest(new
            {
                error = "Tenant already exists",
                tenantId = request.Id
            });
        }

        var tenant = new Tenant
        {
            Id = request.Id,
            Name = request.Name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Plan = request.Plan ?? "Free",
            MaxUsers = request.MaxUsers ?? 10,
            ContactEmail = request.ContactEmail,
            CustomDomain = request.CustomDomain
        };

        await _tenantRepository.CreateAsync(tenant);

        _logger.LogInformation("Tenant created: {TenantId} by admin", tenant.Id);

        return CreatedAtAction(nameof(GetById), new { tenantId = tenant.Id }, tenant);
    }

    /// <summary>
    /// Updates a tenant
    /// </summary>
    [HttpPut("{tenantId}")]
    [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string tenantId, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            return NotFound(new { error = "Tenant not found", tenantId });
        }

        // Update fields
        if (!string.IsNullOrEmpty(request.Name))
            tenant.Name = request.Name;

        if (request.IsActive.HasValue)
            tenant.IsActive = request.IsActive.Value;

        if (!string.IsNullOrEmpty(request.Plan))
            tenant.Plan = request.Plan;

        if (request.MaxUsers.HasValue)
            tenant.MaxUsers = request.MaxUsers.Value;

        if (!string.IsNullOrEmpty(request.ContactEmail))
            tenant.ContactEmail = request.ContactEmail;

        if (request.CustomDomain != null)
            tenant.CustomDomain = request.CustomDomain;

        await _tenantRepository.UpdateAsync(tenant);

        _logger.LogInformation("Tenant updated: {TenantId} by admin", tenantId);

        return Ok(tenant);
    }

    /// <summary>
    /// Deletes (deactivates) a tenant
    /// </summary>
    [HttpDelete("{tenantId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string tenantId)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null)
        {
            return NotFound(new { error = "Tenant not found", tenantId });
        }

        await _tenantRepository.DeleteAsync(tenantId);

        _logger.LogInformation("Tenant deleted: {TenantId} by admin", tenantId);

        return NoContent();
    }

    private static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        // Must be lowercase alphanumeric with hyphens, 3-63 characters
        return System.Text.RegularExpressions.Regex.IsMatch(
            slug,
            @"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$"
        );
    }
}

public record CreateTenantRequest
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Plan { get; init; }
    public int? MaxUsers { get; init; }
    public string ContactEmail { get; init; } = string.Empty;
    public string? CustomDomain { get; init; }
}

public record UpdateTenantRequest
{
    public string? Name { get; init; }
    public bool? IsActive { get; init; }
    public string? Plan { get; init; }
    public int? MaxUsers { get; init; }
    public string? ContactEmail { get; init; }
    public string? CustomDomain { get; init; }
}
