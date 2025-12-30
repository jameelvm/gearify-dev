# Service-to-Service Authentication Implementation Guide

## Overview

This document describes how to implement OAuth 2.0 Client Credentials flow for secure service-to-service authentication in the Gearify microservices architecture.

**Status:** 📋 **Planned** - Not yet implemented
**Priority:** 🔴 **High** - Required for production
**Estimated Time:** 2-3 hours

---

## Current State (As of Dec 2024)

### ❌ What We Have Now
- **No authentication** between microservices
- Services trust each other implicitly
- Only `X-Tenant-Id` header for tenant isolation
- Anyone with network access can call any service

### ⚠️ Security Risks
1. Any compromised service can impersonate another
2. No audit trail of which service made requests
3. Malicious actors can bypass API Gateway
4. Not production-ready

---

## Target Architecture

### ✅ What We'll Implement

**OAuth 2.0 Client Credentials Flow**

```
┌─────────────────┐
│  Catalog Svc    │
│ (Client)        │
└────────┬────────┘
         │ 1. Request token
         │ POST /api/auth/token
         │ { clientId, clientSecret }
         ↓
┌─────────────────┐
│   Auth Service  │
│ (Auth Server)   │
└────────┬────────┘
         │ 2. Returns JWT
         │ { access_token, expires_in }
         ↓
┌─────────────────┐
│  Catalog Svc    │
│ (Client)        │
└────────┬────────┘
         │ 3. Call API with token
         │ Authorization: Bearer {token}
         ↓
┌─────────────────┐
│   Media Svc     │
│ (Resource)      │
└────────┬────────┘
         │ 4. Validate JWT
         │ 5. Process request
         ↓
```

---

## Implementation Steps

### Phase 1: Auth Service Updates

#### Step 1.1: Create Service Client Entity

**File:** `gearify-auth-svc/Domain/Entities/ServiceClient.cs`

```csharp
namespace Gearify.AuthService.Domain.Entities;

/// <summary>
/// Represents a registered service client for OAuth 2.0 Client Credentials
/// </summary>
public class ServiceClient
{
    // DynamoDB Keys
    public string PK { get; set; } = string.Empty;  // CLIENT#{clientId}
    public string SK { get; set; } = string.Empty;  // METADATA

    // Core attributes
    public string ClientId { get; set; } = string.Empty;         // service-catalog
    public string ClientSecret { get; set; } = string.Empty;     // Hashed secret
    public string ServiceName { get; set; } = string.Empty;      // Catalog Service
    public string Description { get; set; } = string.Empty;

    // Permissions
    public List<string> AllowedScopes { get; set; } = new();     // ["media:write", "media:read"]
    public List<string> AllowedServices { get; set; } = new();   // ["media-svc", "search-svc"]

    // Status
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Security
    public int TokenExpirationMinutes { get; set; } = 60;        // Default 1 hour
    public string? IpWhitelist { get; set; }                     // Optional IP restrictions
}
```

#### Step 1.2: Create Token Endpoint

**File:** `gearify-auth-svc/API/Controllers/AuthController.cs`

Add new endpoint:

```csharp
/// <summary>
/// OAuth 2.0 Client Credentials - Get service-to-service token
/// </summary>
[HttpPost("token")]
[ProducesResponseType(typeof(TokenResponse), 200)]
[ProducesResponseType(400)]
[ProducesResponseType(401)]
public async Task<IActionResult> GetServiceToken([FromBody] ClientCredentialsRequest request)
{
    try
    {
        // Validate grant_type
        if (request.GrantType != "client_credentials")
        {
            return BadRequest(new { error = "unsupported_grant_type" });
        }

        // Validate client credentials
        var command = new ValidateServiceClientCommand(
            request.ClientId,
            request.ClientSecret);

        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return Unauthorized(new { error = "invalid_client" });
        }

        // Generate service token (JWT)
        var tokenCommand = new GenerateServiceTokenCommand(
            result.ClientId,
            result.ServiceName,
            result.AllowedScopes);

        var tokenResult = await _mediator.Send(tokenCommand);

        return Ok(new TokenResponse
        {
            AccessToken = tokenResult.Token,
            TokenType = "Bearer",
            ExpiresIn = tokenResult.ExpiresIn,
            Scope = string.Join(" ", result.AllowedScopes)
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error generating service token");
        return StatusCode(500, new { error = "server_error" });
    }
}
```

**DTO:**

```csharp
public record ClientCredentialsRequest(
    string ClientId,
    string ClientSecret,
    string GrantType = "client_credentials");

public record TokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
    public string Scope { get; init; } = string.Empty;
}
```

#### Step 1.3: Register Service Clients

**File:** `gearify-auth-svc/Application/Commands/RegisterServiceClientCommand.cs`

```csharp
public record RegisterServiceClientCommand(
    string ClientId,
    string ServiceName,
    string Description,
    List<string> AllowedScopes,
    List<string> AllowedServices) : IRequest<RegisterServiceClientResult>;

public record RegisterServiceClientResult(
    bool Success,
    string? ClientSecret = null,
    string? ErrorMessage = null);

public class RegisterServiceClientCommandHandler :
    IRequestHandler<RegisterServiceClientCommand, RegisterServiceClientResult>
{
    // Implementation:
    // 1. Generate secure client secret
    // 2. Hash secret with BCrypt
    // 3. Save to DynamoDB
    // 4. Return plaintext secret (only shown once!)
}
```

#### Step 1.4: Seed Initial Service Clients

**File:** `gearify-auth-svc/Data/ServiceClientSeeder.cs`

```json
[
  {
    "clientId": "service-catalog",
    "serviceName": "Catalog Service",
    "description": "Product catalog management service",
    "allowedScopes": ["media:write", "media:read", "media:delete"],
    "allowedServices": ["media-svc"],
    "secret": "GENERATE_SECURE_SECRET"
  },
  {
    "clientId": "service-search",
    "serviceName": "Search Service",
    "description": "Product search and indexing service",
    "allowedScopes": ["catalog:read"],
    "allowedServices": ["catalog-svc"],
    "secret": "GENERATE_SECURE_SECRET"
  }
]
```

---

### Phase 2: Service Client Implementation (Catalog Service Example)

#### Step 2.1: Create Token Manager

**File:** `gearify-catalog-svc/Infrastructure/Auth/ServiceTokenManager.cs`

```csharp
public interface IServiceTokenManager
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public class ServiceTokenManager : IServiceTokenManager
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceTokenManager> _logger;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Check if cached token is still valid
        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
        {
            return _cachedToken;
        }

        // Get new token
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            {
                return _cachedToken;
            }

            // Request new token
            var clientId = _configuration["ServiceAuth:ClientId"];
            var clientSecret = _configuration["ServiceAuth:ClientSecret"];
            var authUrl = _configuration["ServiceAuth:AuthUrl"];

            var request = new
            {
                client_id = clientId,
                client_secret = clientSecret,
                grant_type = "client_credentials"
            };

            var response = await _httpClient.PostAsJsonAsync($"{authUrl}/api/auth/token", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            _logger.LogInformation("Service token acquired, expires at {Expiry}", _tokenExpiry);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

#### Step 2.2: Update MediaServiceClient

**File:** `gearify-catalog-svc/Infrastructure/Clients/MediaServiceClient.cs`

Update the HTTP request to include the token:

```csharp
public class MediaServiceClient : IMediaServiceClient
{
    private readonly IServiceTokenManager _tokenManager;

    public async Task<MediaUploadResponse?> UploadProductImageAsync(...)
    {
        // Get service token
        var token = await _tokenManager.GetTokenAsync(cancellationToken);

        // Add to request
        var request = new HttpRequestMessage(HttpMethod.Post, "api/media/upload");
        request.Headers.Add("X-Tenant-Id", _tenantContext.TenantId);
        request.Headers.Add("Authorization", $"Bearer {token}");  // ← ADD THIS
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        // ...
    }
}
```

#### Step 2.3: Configuration

**File:** `gearify-catalog-svc/appsettings.json`

```json
{
  "ServiceAuth": {
    "ClientId": "service-catalog",
    "ClientSecret": "PLACEHOLDER - Use Secrets Manager in production",
    "AuthUrl": "http://auth-svc:80"
  }
}
```

**File:** `gearify-catalog-svc/appsettings.Development.json`

```json
{
  "ServiceAuth": {
    "ClientId": "service-catalog",
    "ClientSecret": "dev-catalog-secret-xyz123",
    "AuthUrl": "http://localhost:5011"
  }
}
```

---

### Phase 3: Media Service Updates (Resource Server)

#### Step 3.1: Add JWT Validation

**File:** `gearify-media-svc/Startup.cs`

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... existing code ...

    // JWT Authentication
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = Configuration["JwtSettings:Issuer"];
            options.Audience = Configuration["JwtSettings:Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Configuration["JwtSettings:Issuer"],
                ValidAudience = Configuration["JwtSettings:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(Configuration["JwtSettings:Secret"]))
            };
        });

    services.AddAuthorization(options =>
    {
        options.AddPolicy("ServiceAccess", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("client_id"); // Must be a service client
        });
    });
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseMultitenancy();
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseRouting();
    app.UseAuthentication();  // ← ADD THIS
    app.UseAuthorization();   // ← ADD THIS

    app.UseEndpoints(endpoints => { ... });
}
```

#### Step 3.2: Protect Endpoints

**File:** `gearify-media-svc/API/Controllers/MediaController.cs`

```csharp
[ApiController]
[Route("api/media")]
[Authorize(Policy = "ServiceAccess")]  // ← ADD THIS
public class MediaController : ControllerBase
{
    // All endpoints now require valid service token
}
```

---

## Configuration & Secrets Management

### Development Environment

**Store in appsettings.Development.json**

```json
{
  "ServiceAuth": {
    "ClientId": "service-catalog",
    "ClientSecret": "dev-secret-not-for-production"
  }
}
```

### Production Environment

**Use AWS Secrets Manager (already configured in LocalStack)**

```bash
# Store service credentials
aws secretsmanager create-secret \
  --name gearify/services/catalog/credentials \
  --secret-string '{"clientId":"service-catalog","clientSecret":"SECURE_RANDOM_SECRET"}' \
  --region us-east-1

# Retrieve in code
var secret = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
{
    SecretId = "gearify/services/catalog/credentials"
});
```

**Update Startup.cs to read from Secrets Manager in production**

```csharp
if (env.IsProduction())
{
    var credentials = await GetServiceCredentialsFromSecretsManager();
    Configuration["ServiceAuth:ClientId"] = credentials.ClientId;
    Configuration["ServiceAuth:ClientSecret"] = credentials.ClientSecret;
}
```

---

## Testing

### Test 1: Get Service Token

```bash
# Request token
curl -X POST http://localhost:5011/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "client_id": "service-catalog",
    "client_secret": "dev-catalog-secret-xyz123",
    "grant_type": "client_credentials"
  }'

# Expected response
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "media:write media:read media:delete"
}
```

### Test 2: Call Media Service with Token

```bash
# Upload image with service token
curl -X POST http://localhost:5009/api/media/upload \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-Id: tenant-123" \
  -F "file=@test.jpg" \
  -F "entityType=Product" \
  -F "entityId=prod-123"

# Expected: Success
```

### Test 3: Call Without Token (Should Fail)

```bash
curl -X POST http://localhost:5009/api/media/upload \
  -H "X-Tenant-Id: tenant-123" \
  -F "file=@test.jpg"

# Expected: 401 Unauthorized
```

---

## Security Considerations

### Client Secret Generation

```csharp
// Generate cryptographically secure secret
public static string GenerateClientSecret()
{
    var randomBytes = new byte[32];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(randomBytes);
    return Convert.ToBase64String(randomBytes);
}
```

### Secret Storage

- ✅ **Development:** appsettings.Development.json
- ✅ **Production:** AWS Secrets Manager
- ❌ **Never:** appsettings.json (committed to git)
- ❌ **Never:** Plain text in code

### Token Expiration

- **Recommended:** 1 hour (3600 seconds)
- **Minimum:** 15 minutes
- **Maximum:** 24 hours

### Scope Design

```json
{
  "media:read": "Read media metadata and files",
  "media:write": "Upload and modify media",
  "media:delete": "Delete media files",
  "catalog:read": "Read product catalog",
  "catalog:write": "Modify product catalog"
}
```

---

## Migration Strategy

### Option 1: Big Bang (Not Recommended)
- Implement all services at once
- High risk, hard to rollback

### Option 2: Phased Rollout (Recommended)

**Phase 1: Add but don't enforce**
- Week 1: Add token generation (Auth Service)
- Week 2: Add token acquisition (client services)
- Week 3: Add token validation but make it optional
- **Test thoroughly in development**

**Phase 2: Enforce gradually**
- Week 4: Require auth on Media Service only
- Week 5: Require auth on all services
- **Monitor error rates**

**Phase 3: Cleanup**
- Week 6: Remove fallback/optional code
- Week 7: Security audit

---

## Performance Impact

### Token Acquisition
- **First call:** ~50ms (get token from Auth Service)
- **Cached calls:** <1ms (use cached token)
- **Token refresh:** ~50ms (every hour)

### Token Validation
- **JWT validation:** ~1-2ms per request
- **No database lookup** (signature validation only)

### Expected Impact
- **Negligible** (<5ms per request on average)
- Token caching minimizes overhead

---

## Monitoring & Observability

### Metrics to Track

```csharp
// Token acquisition metrics
- service_token_requests_total
- service_token_errors_total
- service_token_cache_hits_total
- service_token_cache_misses_total

// Authentication metrics
- service_auth_requests_total
- service_auth_failures_total (by reason)
- service_auth_latency_seconds
```

### Logs to Add

```csharp
_logger.LogInformation("Service token acquired for {ClientId}, expires {Expiry}", clientId, expiry);
_logger.LogWarning("Service token validation failed: {Reason}", reason);
_logger.LogError("Failed to acquire service token: {Error}", error);
```

### Alerts

- ⚠️ Service auth failure rate > 5%
- 🔴 Cannot acquire service token for > 1 minute
- ⚠️ Token cache hit rate < 95%

---

## Rollback Plan

### If Issues Occur

1. **Disable enforcement immediately:**
   ```csharp
   // In Media Service Startup.cs
   [AllowAnonymous]  // ← Add this to bypass temporarily
   public class MediaController
   ```

2. **Monitor error logs** to identify root cause

3. **Fix issue** in non-production environment

4. **Re-enable gradually** following phased rollout

---

## Additional Resources

### Standards & RFCs
- [RFC 6749: OAuth 2.0 Authorization Framework](https://tools.ietf.org/html/rfc6749)
- [RFC 6750: OAuth 2.0 Bearer Token Usage](https://tools.ietf.org/html/rfc6750)

### Libraries
- **Microsoft.AspNetCore.Authentication.JwtBearer** (NuGet)
- **System.IdentityModel.Tokens.Jwt** (NuGet)

### Tools
- [jwt.io](https://jwt.io) - Decode and verify JWTs
- [OAuth 2.0 Playground](https://www.oauth.com/playground/)

---

## Implementation Checklist

### Auth Service
- [ ] Create ServiceClient entity
- [ ] Create /api/auth/token endpoint
- [ ] Add client validation logic
- [ ] Seed initial service clients
- [ ] Add to DynamoDB table

### Client Services (Catalog, Search, etc.)
- [ ] Create ServiceTokenManager
- [ ] Update HTTP clients to include token
- [ ] Add configuration settings
- [ ] Store secrets properly
- [ ] Add error handling

### Resource Services (Media, etc.)
- [ ] Add JWT authentication middleware
- [ ] Add authorization policies
- [ ] Protect endpoints with [Authorize]
- [ ] Add scope validation
- [ ] Test with valid/invalid tokens

### Testing
- [ ] Unit tests for token generation
- [ ] Integration tests for token flow
- [ ] Load tests for performance
- [ ] Security tests (invalid tokens, etc.)

### Deployment
- [ ] Update docker-compose with secrets
- [ ] Configure AWS Secrets Manager
- [ ] Update CI/CD pipeline
- [ ] Document runbooks

---

## Questions to Answer Before Implementation

1. **Token expiration time?**
   - Recommendation: 1 hour
   - Your choice: _________

2. **Scope granularity?**
   - Service-level (media:*, catalog:*)?
   - Action-level (media:read, media:write)?
   - Your choice: _________

3. **Secret rotation strategy?**
   - Manual on demand?
   - Automated quarterly?
   - Your choice: _________

4. **Production secrets storage?**
   - AWS Secrets Manager (recommended)
   - Environment variables?
   - Your choice: _________

---

## Status Tracking

| Component | Status | Notes |
|-----------|--------|-------|
| Auth Service - Token Endpoint | ⏳ Not Started | |
| Auth Service - Client Registration | ⏳ Not Started | |
| Catalog Service - Token Manager | ⏳ Not Started | |
| Catalog Service - HTTP Client Update | ⏳ Not Started | |
| Media Service - JWT Validation | ⏳ Not Started | |
| Media Service - Endpoint Protection | ⏳ Not Started | |
| Testing | ⏳ Not Started | |
| Documentation | ✅ Complete | This file |

---

**Created:** December 27, 2024
**Last Updated:** December 27, 2024
**Owner:** Development Team
**Estimated Implementation Time:** 2-3 hours
**Priority:** High (required for production)

---

## Quick Start When Ready

When you're ready to implement, tell Claude:

> "Implement service-to-service authentication as documented in service-to-service-authentication.md"

Claude will follow this guide step-by-step to implement the entire OAuth 2.0 Client Credentials flow.
