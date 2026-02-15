using Gearify.SharedKernel.Middleware;
using Microsoft.AspNetCore.Builder;

namespace Gearify.SharedKernel.Extensions;

/// <summary>
/// Extension methods for configuring correlation ID propagation.
/// </summary>
public static class CorrelationExtensions
{
    /// <summary>
    /// Adds the correlation ID middleware to the request pipeline.
    /// Reads X-Correlation-ID from the request (or generates a new one),
    /// stores it in CorrelationContext for downstream code, and echoes it
    /// back in the response header.
    /// Should be called early in the pipeline, before other middleware.
    /// </summary>
    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationMiddleware>();
        return app;
    }
}
