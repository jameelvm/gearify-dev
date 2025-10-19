# Multi-Tenancy Pattern Guide

## Overview

This guide explains the **Middleware + Scoped DI** pattern for handling multi-tenancy in Gearify microservices. This pattern eliminates the need to pass `tenantId` as a parameter throughout your codebase.

## The Problem

### Before (❌ Repetitive and Error-Prone)

```csharp
// Controller - must extract tenant from header EVERY TIME
[HttpPost("{userId}/items")]
public async Task<IActionResult> AddToCart(
    string userId,
    [FromBody] AddItemRequest request,
    [FromHeader(Name = "X-Tenant-Id")] string tenantId)  // ❌ Repetitive!
{
    var result = await _mediator.Send(new AddToCartCommand(
        userId,
        tenantId,  // ❌ Must pass everywhere
        request.ProductId,
        ...
    ));
}

// Command - must include tenantId
public record AddToCartCommand(
    string UserId,
    string TenantId,  // ❌ Clutters the API
    string ProductId,
    ...
) : IRequest<AddToCartResult>;

// Handler - must receive tenantId as parameter
public async Task<AddToCartResult> Handle(AddToCartCommand request, ...)
{
    var cart = await _repository.GetCartAsync(request.UserId, request.TenantId);
    // ...
}
```

**Problems:**
- 🔴 **Repetitive** - `[FromHeader]` in every controller method
- 🔴 **Error-prone** - Easy to forget in new endpoints
- 🔴 **Pollutes** domain model with infrastructure concerns
- 🔴 **Hard to test** - Must mock HTTP headers everywhere
- 🔴 **Violates DRY** - Same code repeated everywhere

## The Solution

### After (✅ Clean and Maintainable)

```csharp
// Controller - NO tenant parameter!
[HttpPost("{userId}/items")]
public async Task<IActionResult> AddToCart(string userId, [FromBody] AddItemRequest request)
{
    var result = await _mediator.Send(new AddToCartCommand(
        userId,
        request.ProductId,
        ...
    ));
    // Tenant is automatically available via ITenantContext!
}

// Command - NO tenantId!
public record AddToCartCommand(
    string UserId,
    string ProductId,
    ...
) : IRequest<AddToCartResult>;

// Handler - inject ITenantContext
public class AddToCartCommandHandler
{
    private readonly ITenantContext _tenantContext;  // ✅ Injected!

    public async Task<AddToCartResult> Handle(AddToCartCommand request, ...)
    {
        var tenantId = _tenantContext.TenantId;  // ✅ Get from context!
        var cart = await _repository.GetCartAsync(request.UserId, tenantId);
        // ...
    }
}
```

**Benefits:**
- ✅ **DRY** - Tenant extraction happens once in middleware
- ✅ **Clean** - No infrastructure concerns in domain layer
- ✅ **Type-safe** - Compile-time checks
- ✅ **Testable** - Easy to mock `ITenantContext`
- ✅ **Flexible** - Easy to change tenant resolution strategy

## Architecture

### Components

```
HTTP Request with X-Tenant-Id header
        ↓
┌───────────────────────────────┐
│    TenantMiddleware           │ ← Extracts tenant from header
│  (runs for every request)     │
└───────────────────────────────┘
        ↓
┌───────────────────────────────┐
│     TenantContext (Scoped)    │ ← Stores tenant for this request
│  ITenantContext.TenantId      │
└───────────────────────────────┘
        ↓
┌───────────────────────────────┐
│   Controllers, Handlers       │ ← Inject ITenantContext
│   Repositories, Services      │   to access tenant
└───────────────────────────────┘
```

### File Structure

```
gearify-shared-kernel/
├── Multitenancy/
│   ├── ITenantContext.cs           # Interface for accessing tenant
│   └── TenantContext.cs            # Implementation (scoped per request)
├── Middleware/
│   └── TenantMiddleware.cs         # Extracts tenant from HTTP header
└── Extensions/
    └── MultitenancyExtensions.cs   # Registration helpers
```

## Implementation Details

### 1. ITenantContext Interface

```csharp
namespace Gearify.SharedKernel.Multitenancy;

public interface ITenantContext
{
    /// <summary>
    /// The current tenant ID for this request.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Indicates whether the tenant has been successfully resolved.
    /// </summary>
    bool IsResolved { get; }
}
```

**Purpose**: Provides a clean abstraction for accessing tenant information.

### 2. TenantContext Implementation

```csharp
namespace Gearify.SharedKernel.Multitenancy;

public class TenantContext : ITenantContext
{
    private string _tenantId = string.Empty;
    private bool _isResolved = false;

    public string TenantId => _tenantId;
    public bool IsResolved => _isResolved;

    /// <summary>
    /// Sets the tenant ID for the current request.
    /// This should only be called by the TenantMiddleware.
    /// </summary>
    public void SetTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty");
        }

        _tenantId = tenantId;
        _isResolved = true;
    }

    /// <summary>
    /// Clears the tenant context (used for testing)
    /// </summary>
    public void Clear()
    {
        _tenantId = string.Empty;
        _isResolved = false;
    }
}
```

**Lifetime**: Registered as **Scoped** - one instance per HTTP request.

### 3. TenantMiddleware

```csharp
namespace Gearify.SharedKernel.Middleware;

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
        // Skip tenant resolution for health checks and swagger
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        // Extract tenant ID from header
        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantIdHeader))
        {
            _logger.LogWarning("Missing {HeaderName} header", TenantHeaderName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Missing required header",
                message = $"{TenantHeaderName} header is required"
            });
            return;
        }

        var tenantId = tenantIdHeader.ToString();

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("Empty {HeaderName} header", TenantHeaderName);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid tenant ID",
                message = $"{TenantHeaderName} header cannot be empty"
            });
            return;
        }

        // Set the tenant in the scoped context
        ((TenantContext)tenantContext).SetTenant(tenantId);

        _logger.LogDebug("Tenant {TenantId} resolved", tenantId);

        // Continue with the request pipeline
        await _next(context);
    }
}
```

**Key Features:**
- Extracts tenant from `X-Tenant-Id` header
- Returns 400 Bad Request if header is missing or empty
- Skips tenant check for health and swagger endpoints
- Logs tenant resolution for debugging

### 4. Extension Methods

```csharp
namespace Gearify.SharedKernel.Extensions;

public static class MultitenancyExtensions
{
    /// <summary>
    /// Adds multi-tenancy services to the DI container.
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
```

## Usage

### Step 1: Register Services in Program.cs

```csharp
using Gearify.SharedKernel.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ... other services ...

// Add multitenancy support
builder.Services.AddMultitenancy();

var app = builder.Build();

// Add tenant resolution middleware (MUST be before controllers)
app.UseMultitenancy();

app.MapControllers();
app.Run();
```

**⚠️ Important:** `UseMultitenancy()` must be called **before** `MapControllers()` to ensure tenant is resolved before reaching your controllers.

### Step 2: Update Commands/Queries

**Remove `TenantId` parameter:**

```csharp
// Before
public record AddToCartCommand(
    string UserId,
    string TenantId,  // ❌ Remove this
    string ProductId,
    ...
) : IRequest<AddToCartResult>;

// After
public record AddToCartCommand(
    string UserId,
    string ProductId,
    ...
) : IRequest<AddToCartResult>;
```

### Step 3: Update Handlers

**Inject `ITenantContext` and use it:**

```csharp
using Gearify.SharedKernel.Multitenancy;

public class AddToCartCommandHandler : IRequestHandler<AddToCartCommand, AddToCartResult>
{
    private readonly ICartRepository _repository;
    private readonly ITenantContext _tenantContext;  // ✅ Inject
    private readonly ILogger<AddToCartCommandHandler> _logger;

    public AddToCartCommandHandler(
        ICartRepository repository,
        ITenantContext tenantContext,  // ✅ Inject
        ILogger<AddToCartCommandHandler> logger)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AddToCartResult> Handle(AddToCartCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;  // ✅ Get tenant

        var cart = await _repository.GetCartAsync(request.UserId, tenantId);
        // ... rest of logic
    }
}
```

### Step 4: Update Controllers

**Remove `[FromHeader]` parameter:**

```csharp
// Before
[HttpPost("{userId}/items")]
public async Task<IActionResult> AddToCart(
    string userId,
    [FromBody] AddItemRequest request,
    [FromHeader(Name = "X-Tenant-Id")] string tenantId)  // ❌ Remove
{
    var result = await _mediator.Send(new AddToCartCommand(
        userId,
        tenantId,  // ❌ Remove
        request.ProductId,
        ...
    ));
}

// After
[HttpPost("{userId}/items")]
public async Task<IActionResult> AddToCart(string userId, [FromBody] AddItemRequest request)
{
    var result = await _mediator.Send(new AddToCartCommand(
        userId,
        request.ProductId,
        ...
    ));
    // Tenant is automatically resolved!
}
```

## Testing

### Unit Testing with Mock

```csharp
using Gearify.SharedKernel.Multitenancy;
using Moq;
using Xunit;

public class AddToCartCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_AddItemToCart()
    {
        // Arrange
        var mockRepository = new Mock<ICartRepository>();
        var mockTenantContext = new Mock<ITenantContext>();
        var mockLogger = new Mock<ILogger<AddToCartCommandHandler>>();

        mockTenantContext.Setup(x => x.TenantId).Returns("test-tenant");
        mockTenantContext.Setup(x => x.IsResolved).Returns(true);

        var handler = new AddToCartCommandHandler(
            mockRepository.Object,
            mockTenantContext.Object,
            mockLogger.Object
        );

        var command = new AddToCartCommand("user1", "prod1", "Product", "SKU1", 5, 99.99m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        mockRepository.Verify(x => x.GetCartAsync("user1", "test-tenant"), Times.Once);
    }
}
```

### Integration Testing

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;
using Xunit;

public class CartControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CartControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddToCart_WithTenantHeader_ReturnsSuccess()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "test-tenant");

        var request = new { ProductId = "prod1", Quantity = 5, Price = 99.99m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart/user1/items", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AddToCart_WithoutTenantHeader_Returns400()
    {
        // Arrange - NO tenant header

        var request = new { ProductId = "prod1", Quantity = 5, Price = 99.99m };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart/user1/items", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

## Advanced Scenarios

### Scenario 1: Tenant from JWT Claim (Instead of Header)

```csharp
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Extract from JWT claim instead of header
        var tenantClaim = context.User.FindFirst("tenant_id");

        if (tenantClaim == null)
        {
            context.Response.StatusCode = 401;
            return;
        }

        ((TenantContext)tenantContext).SetTenant(tenantClaim.Value);
        await _next(context);
    }
}
```

### Scenario 2: Tenant from Subdomain

```csharp
public class TenantMiddleware
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // Extract from subdomain: tenant1.api.example.com
        var host = context.Request.Host.Host;
        var parts = host.Split('.');

        if (parts.Length < 3)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var tenantId = parts[0];  // First part is tenant
        ((TenantContext)tenantContext).SetTenant(tenantId);
        await _next(context);
    }
}
```

### Scenario 3: Tenant Validation

```csharp
public class TenantMiddleware
{
    private readonly ITenantRepository _tenantRepository;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].ToString();

        // Validate tenant exists in database
        var tenant = await _tenantRepository.GetByIdAsync(tenantId);

        if (tenant == null || !tenant.IsActive)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Invalid or inactive tenant"
            });
            return;
        }

        ((TenantContext)tenantContext).SetTenant(tenantId);
        await _next(context);
    }
}
```

## Comparison with Alternatives

### Alternative 1: Static AsyncLocal (❌ Not Recommended)

```csharp
public class TenantContext
{
    private static readonly AsyncLocal<string?> _tenantId = new();

    public static string? Current
    {
        get => _tenantId.Value;
        set => _tenantId.Value = value;
    }
}

// Usage (no DI)
var tenantId = TenantContext.Current;
```

**Problems:**
- ❌ Static state is hard to test
- ❌ No compile-time safety
- ❌ Can leak across requests if not careful
- ❌ Violates dependency injection principles

### Alternative 2: IHttpContextAccessor (❌ Not Recommended)

```csharp
public class AddToCartCommandHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task Handle(...)
    {
        var tenantId = _httpContextAccessor.HttpContext.Request.Headers["X-Tenant-Id"];
    }
}
```

**Problems:**
- ❌ Couples application layer to HTTP
- ❌ Breaks Clean Architecture
- ❌ Hard to test (requires HTTP context)
- ❌ Not usable in background jobs/message handlers

### Alternative 3: Middleware + Scoped DI (✅ Recommended - Current Implementation)

**Advantages:**
- ✅ Clean Architecture - no HTTP coupling
- ✅ Easy to test - mock `ITenantContext`
- ✅ Type-safe - compile-time checks
- ✅ Flexible - easy to change resolution strategy
- ✅ Works in all contexts (HTTP, background jobs, etc.)

## Troubleshooting

### Issue 1: "ITenantContext not resolved"

**Cause**: Forgot to call `builder.Services.AddMultitenancy()`

**Solution**:
```csharp
// In Program.cs
builder.Services.AddMultitenancy();  // ✅ Add this
```

### Issue 2: "TenantId is empty"

**Cause**: Forgot to call `app.UseMultitenancy()` or called it after `app.MapControllers()`

**Solution**:
```csharp
// In Program.cs
app.UseMultitenancy();  // ✅ Must be BEFORE MapControllers
app.MapControllers();
```

### Issue 3: "Cannot cast to TenantContext"

**Cause**: Trying to call `SetTenant()` outside of middleware

**Solution**: Only middleware should call `SetTenant()`. In handlers, only read `TenantId` property.

## Summary

| Aspect | Before | After |
|--------|--------|-------|
| **Tenant parameter** | In every command | ❌ None |
| **Controller** | `[FromHeader]` everywhere | ✅ Clean |
| **Handler** | Must receive tenant param | ✅ Inject `ITenantContext` |
| **Testability** | Mock HTTP headers | ✅ Mock interface |
| **DRY** | Repeated code | ✅ Single middleware |
| **Clean Architecture** | HTTP concerns leak | ✅ Properly isolated |

## Gearify Implementation Status

✅ **Implemented:**
- Shared Kernel with `ITenantContext`, `TenantContext`, `TenantMiddleware`
- Cart Service (commands, queries, controller updated)
- Payment Service (commands updated)
- Order Service (commands updated)
- Search Service (queries updated)

⚠️ **Next Steps for Other Services:**
1. Add SharedKernel project reference
2. Update Program.cs (add `AddMultitenancy()` and `UseMultitenancy()`)
3. Remove `TenantId` parameters from commands/queries
4. Inject `ITenantContext` in handlers
5. Remove `[FromHeader(Name = "X-Tenant-Id")]` from controllers

## Further Reading

- [Multi-Tenancy in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/multi-tenancy)
- [Scoped Services in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection#service-lifetimes)
- [Middleware in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware)
