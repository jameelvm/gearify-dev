using System.Security.Claims;
using System.Text.RegularExpressions;
using Gearify.SharedKernel.AI.Events;

namespace Gearify.ApiGateway.Middleware;

/// <summary>
/// Intercepts HTTP responses to detect user interactions (product views, cart additions,
/// purchases, searches) and publishes them as events to SQS. Runs fire-and-forget
/// to never block or delay the user response.
/// </summary>
public class EventTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EventTrackingMiddleware> _logger;

    // Pattern: GET /api/catalog/products/{id}
    private static readonly Regex ProductViewPattern =
        new(@"^/api/catalog/products/([a-zA-Z0-9\-]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public EventTrackingMiddleware(RequestDelegate next, ILogger<EventTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IUserInteractionPublisher publisher)
    {
        // Let the request flow through the pipeline first
        await _next(context);

        // Only track successful responses
        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
            return;

        var interaction = DetectInteraction(context);
        if (interaction is null)
            return;

        // Fire-and-forget: never delay the response for event tracking
        _ = Task.Run(async () =>
        {
            try
            {
                await publisher.PublishAsync(interaction);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fire-and-forget event publish failed for {EventType}", interaction.EventType);
            }
        });
    }

    private UserInteractionEvent? DetectInteraction(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        string? eventType = null;
        string? productId = null;

        // GET /api/catalog/products/{id} → View
        if (method == "GET")
        {
            var viewMatch = ProductViewPattern.Match(path);
            if (viewMatch.Success)
            {
                eventType = InteractionEventTypes.View;
                productId = viewMatch.Groups[1].Value;
            }
            // GET /api/search/* → Search
            else if (path.StartsWith("/api/search", StringComparison.OrdinalIgnoreCase))
            {
                eventType = InteractionEventTypes.Search;
            }
        }
        // POST /api/cart/items → AddToCart
        else if (method == "POST" && path.StartsWith("/api/cart/items", StringComparison.OrdinalIgnoreCase))
        {
            eventType = InteractionEventTypes.AddToCart;
        }
        // POST /api/orders → Purchase
        else if (method == "POST" && path.Equals("/api/orders", StringComparison.OrdinalIgnoreCase))
        {
            eventType = InteractionEventTypes.Purchase;
        }

        if (eventType is null)
            return null;

        var userId = ExtractUserId(context);
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
        var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault()
                        ?? context.TraceIdentifier;

        var metadata = new Dictionary<string, string>();

        var referrer = context.Request.Headers["Referer"].FirstOrDefault();
        if (!string.IsNullOrEmpty(referrer))
            metadata["referrer"] = referrer;

        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userAgent))
            metadata["userAgent"] = userAgent;

        // Capture search query for Search events
        if (eventType == InteractionEventTypes.Search)
        {
            var query = context.Request.Query["q"].FirstOrDefault()
                        ?? context.Request.Query["query"].FirstOrDefault();
            if (!string.IsNullOrEmpty(query))
                metadata["searchQuery"] = query;
        }

        return new UserInteractionEvent
        {
            UserId = userId,
            ProductId = productId,
            TenantId = tenantId,
            EventType = eventType,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            Metadata = metadata
        };
    }

    private static string ExtractUserId(HttpContext context)
    {
        // Try JWT claims first
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            return userId;

        // Fallback to X-User-Id header
        var headerUserId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(headerUserId))
            return headerUserId;

        return "anonymous";
    }
}
