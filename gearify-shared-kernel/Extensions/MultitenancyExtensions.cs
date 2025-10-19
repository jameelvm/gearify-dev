using Gearify.SharedKernel.Middleware;
using Gearify.SharedKernel.Multitenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gearify.SharedKernel.Extensions;

/// <summary>
/// Extension methods for configuring multi-tenancy support.
/// </summary>
public static class MultitenancyExtensions
{
    /// <summary>
    /// Adds multi-tenancy services to the dependency injection container.
    /// Registers ITenantContext as a scoped service.
    /// </summary>
    public static IServiceCollection AddMultitenancy(this IServiceCollection services)
    {
        // Register TenantContext as scoped - one instance per HTTP request
        services.AddScoped<ITenantContext, TenantContext>();

        return services;
    }

    /// <summary>
    /// Adds the tenant resolution middleware to the request pipeline.
    /// This should be called early in the pipeline, before authentication.
    /// </summary>
    public static IApplicationBuilder UseMultitenancy(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantMiddleware>();
        return app;
    }
}
