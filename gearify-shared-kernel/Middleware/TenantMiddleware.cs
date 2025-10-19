using Gearify.SharedKernel.Multitenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gearify.SharedKernel.Middleware;

/// <summary>
/// Middleware that extracts the tenant ID from the X-Tenant-Id header
/// and populates the ITenantContext for the current request.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;
    private const string TenantHeaderName = "X-Tenant-Id";

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Skip tenant resolution for health checks and other internal endpoints
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from header
        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdHeader))
        {
            _logger.LogWarning("Missing {HeaderName} header for request {Path}",
                TenantHeaderName, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Missing required header",
                message = $"{TenantHeaderName} header is required for all API requests",
                statusCode = 400
            });
            return;
        }

        var tenantId = tenantIdHeader.ToString();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("Empty {HeaderName} header for request {Path}",
                TenantHeaderName, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid tenant ID",
                message = $"{TenantHeaderName} header cannot be empty",
                statusCode = 400
            });
            return;
        }

        try
        {
            // Set the tenant in the scoped context
            ((TenantContext)tenantContext).SetTenant(tenantId);

            _logger.LogDebug("Tenant {TenantId} resolved for request {Path}",
                tenantId, context.Request.Path);

            // Continue with the request pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request for tenant {TenantId}", tenantId);
            throw;
        }
    }
}
