using Serilog.Context;

namespace Gearify.ApiGateway.Middleware;

/// <summary>
/// Middleware that ensures every request has a CorrelationId.
/// Reads from X-Correlation-ID header or generates a new GUID.
/// Echoes the ID back in the response header and pushes it into Serilog LogContext.
/// </summary>
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
