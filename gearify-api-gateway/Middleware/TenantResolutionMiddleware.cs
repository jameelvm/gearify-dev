using Microsoft.Extensions.Primitives;

namespace Gearify.ApiGateway.Middleware;

/// <summary>
/// Middleware that extracts tenant ID from subdomain and adds it as X-Tenant-Id header
/// for downstream services. Falls back to existing X-Tenant-Id header if no subdomain found.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private const string TenantHeaderName = "X-Tenant-Id";

    // Domains that should not be treated as tenant subdomains
    private static readonly HashSet<string> ReservedSubdomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "www", "api", "admin", "app", "staging", "dev", "localhost"
    };

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = ResolveTenantId(context);

        // Add or override X-Tenant-Id header for downstream services
        if (!string.IsNullOrEmpty(tenantId))
        {
            context.Request.Headers[TenantHeaderName] = new StringValues(tenantId);
            _logger.LogDebug("Resolved tenant: {TenantId} from {Source}",
                tenantId,
                GetResolutionSource(context));
        }

        await _next(context);
    }

    private string? ResolveTenantId(HttpContext context)
    {
        // Priority 1: Check if X-Tenant-Id header already exists (for API calls, mobile apps)
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var existingTenantHeader)
            && !string.IsNullOrWhiteSpace(existingTenantHeader))
        {
            return existingTenantHeader.ToString();
        }

        // Priority 2: Extract from subdomain
        var host = context.Request.Host.Host;
        var tenantFromSubdomain = ExtractTenantFromSubdomain(host);

        if (!string.IsNullOrEmpty(tenantFromSubdomain))
        {
            return tenantFromSubdomain;
        }

        // NO DEFAULT FALLBACK - tenant is required
        _logger.LogWarning("No tenant found in subdomain or header for request {Path}", context.Request.Path);
        return null;
    }

    private string? ExtractTenantFromSubdomain(string host)
    {
        // Handle localhost and IP addresses
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.StartsWith("127.0.0.1")
            || host.StartsWith("::1"))
        {
            return null;
        }

        // Split the host into parts
        var hostParts = host.Split('.');

        // Need at least 3 parts for subdomain: tenant.domain.com
        // Or 4 for services like tenant.localhost.direct
        if (hostParts.Length < 2)
        {
            return null;
        }

        var potentialTenant = hostParts[0];

        // Check if it's a reserved subdomain
        if (ReservedSubdomains.Contains(potentialTenant))
        {
            return null;
        }

        // Valid subdomain found
        _logger.LogInformation("Extracted tenant '{Tenant}' from host '{Host}'", potentialTenant, host);
        return potentialTenant;
    }

    private string GetResolutionSource(HttpContext context)
    {
        if (context.Request.Headers.ContainsKey(TenantHeaderName))
        {
            return "Header";
        }

        var host = context.Request.Host.Host;
        var tenantFromSubdomain = ExtractTenantFromSubdomain(host);

        if (!string.IsNullOrEmpty(tenantFromSubdomain))
        {
            return $"Subdomain ({host})";
        }

        return "Default";
    }
}
