# No Default Tenant - Strict Tenant Requirement

## Overview

Gearify now **requires** a valid tenant for all requests. There is **no default fallback** - every access must specify a tenant via subdomain or header.

## What Changed

### ❌ Old Behavior (With Default Fallback)
```
http://localhost:4200 → tenant: "default" ✅
http://invalid.localhost.direct:4200 → tenant: "invalid" ✅ (created on-the-fly)
```

### ✅ New Behavior (No Default)
```
http://localhost:4200 → ERROR: No tenant ❌
http://invalid.localhost.direct:4200 → ERROR: Tenant not found ❌
http://acme.localhost.direct:4200 → tenant: "acme" ✅ (if exists in DB)
```

## Why This Change?

1. **Data Integrity** - Prevents accidental data creation in wrong tenant
2. **Security** - Forces explicit tenant specification
3. **Production Ready** - Matches real-world SaaS behavior
4. **Clear Errors** - Users immediately know they need a tenant

## Auto-Seeding on Startup

Tenants are automatically seeded when you run `docker-compose up`:

### Seeded Tenants:
- **default** - Default Organization (Free plan, 10 users)
- **acme** - Acme Corporation (Pro plan, 50 users)
- **contoso** - Contoso Ltd (Enterprise plan, 200 users)
- **fabrikam** - Fabrikam Inc (Pro plan, 100 users)
- **demo** - Demo Tenant (Free plan, 5 users)

### How It Works:
```bash
docker-compose up
    ↓
LocalStack container starts
    ↓
Auto-runs: /etc/localstack/init/ready.d/seed-tenants.sh
    ↓
Creates gearify-tenants table
    ↓
Seeds 5 test tenants
    ↓
Tenants ready to use! ✅
```

## Access Patterns

### ✅ Valid Access
```
http://default.localhost.direct:4200
http://acme.localhost.direct:4200
http://contoso.localhost.direct:4200
http://fabrikam.localhost.direct:4200
http://demo.localhost.direct:4200
```

### ❌ Invalid Access (Shows Error Page)
```
http://localhost:4200                    → No tenant in URL
http://xyz.localhost.direct:4200         → Tenant not in database
http://127.0.0.1:4200                    → No tenant in URL
http://www.localhost.direct:4200         → Reserved subdomain
```

## Error Page

When no tenant or invalid tenant is detected, users see:

```
🏢 Tenant Not Found

No tenant found in URL. Please use a subdomain to access the application.

You must access the application using a tenant subdomain.

Available tenants (development):
• default.localhost.direct:4200
• acme.localhost.direct:4200
• contoso.localhost.direct:4200
• fabrikam.localhost.direct:4200
• demo.localhost.direct:4200

⚠️ Note: Plain localhost:4200 will not work - you must use a subdomain.
```

## Development Workflow

### Starting Fresh

```bash
# 1. Start services (auto-seeds tenants)
cd gearify-umbrella
docker-compose up -d

# 2. Wait for LocalStack to be ready
docker logs -f gearify-localstack | grep "tenant"

# 3. Verify tenants were seeded
curl http://localhost:8080/api/tenants/list

# 4. Access application
# Open: http://default.localhost.direct:4200
```

### Creating New Tenants

**Option 1: Via API**
```bash
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "id": "newcorp",
    "name": "New Corporation",
    "plan": "Pro",
    "maxUsers": 100,
    "contactEmail": "admin@newcorp.com"
  }'

# Access immediately:
# http://newcorp.localhost.direct:4200
```

**Option 2: Via DynamoDB**
```bash
aws dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "PK": {"S": "TENANT#mycompany"},
    "SK": {"S": "TENANT#mycompany"},
    "Id": {"S": "mycompany"},
    "Name": {"S": "My Company"},
    "IsActive": {"BOOL": true},
    "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "Plan": {"S": "Free"},
    "MaxUsers": {"N": "10"},
    "ContactEmail": {"S": "admin@mycompany.com"}
  }' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

### Re-seeding Tenants

If you need to reset tenants:

```bash
# Delete and recreate tenants
cd gearify-umbrella
docker-compose exec localstack bash /etc/localstack/init/ready.d/seed-tenants.sh
```

## API Behavior

### Angular Frontend
- **Validates tenant on app initialization**
- **Blocks app load if no tenant**
- **Shows error page for invalid tenants**

```typescript
// app.config.ts - APP_INITIALIZER
if (!tenantId) {
  console.error('No tenant detected in URL');
  router.navigate(['/tenant-not-found']);
  return false; // Blocks app initialization
}
```

### API Gateway
- **Extracts tenant from subdomain or header**
- **Logs warning if no tenant found**
- **Downstream services receive X-Tenant-Id header**

```csharp
// TenantResolutionMiddleware.cs
private string? ResolveTenantId(HttpContext context)
{
    // Try header first
    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var header))
        return header.ToString();

    // Try subdomain
    var tenant = ExtractTenantFromSubdomain(host);
    if (!string.IsNullOrEmpty(tenant))
        return tenant;

    // NO DEFAULT FALLBACK
    return null; // Tenant is required!
}
```

### Tenant Service
- **Validates tenant exists in DynamoDB**
- **Returns 404 for invalid tenants**
- **No hardcoded list - all from database**

```csharp
// TenantValidationController.cs
public async Task<IActionResult> ValidateTenant(string tenantId)
{
    var isValid = await _tenantRepository.ExistsAndActiveAsync(tenantId);

    if (!isValid)
        return NotFound(new { error = "Tenant not found" });

    return Ok(new { tenantId, isValid = true, isActive = true });
}
```

## Testing

### Test Case 1: Valid Tenant
```bash
# Access valid tenant
open http://acme.localhost.direct:4200

# Expected: App loads normally
# Check console: "Validating tenant: acme"
# Check console: "Tenant acme validated successfully"
```

### Test Case 2: Invalid Tenant
```bash
# Access non-existent tenant
open http://invalid.localhost.direct:4200

# Expected: Tenant Not Found error page
# Check console: "Invalid tenant: invalid"
```

### Test Case 3: No Tenant
```bash
# Access without subdomain
open http://localhost:4200

# Expected: Tenant Not Found error page
# Check console: "No tenant detected in URL"
```

### Test Case 4: API Call
```bash
# Valid tenant
curl -H "X-Tenant-Id: acme" http://localhost:8080/api/auth/me

# Invalid tenant
curl -H "X-Tenant-Id: invalid" http://localhost:8080/api/auth/me
# Expected: 400 Bad Request (from backend tenant middleware)

# No tenant header
curl http://localhost:8080/api/auth/me
# Expected: 400 Bad Request (from backend tenant middleware)
```

## Migration Checklist

If you're migrating from hardcoded tenants:

- [x] Removed hardcoded tenant lists
- [x] Created DynamoDB tenant table
- [x] Created tenant repository
- [x] Updated validation to use database
- [x] Removed default tenant fallback
- [x] Added auto-seeding on startup
- [x] Updated error pages
- [x] Updated documentation

## Production Considerations

### 1. Tenant Onboarding Flow
Instead of manual tenant creation, implement:
- Registration form
- Email verification
- Payment processing
- Automated provisioning

### 2. Tenant Activation Workflow
```
User signs up → Email verification → Payment → Tenant activated
```

### 3. Tenant Suspension
```csharp
// Deactivate tenant for non-payment
await _tenantRepository.UpdateAsync(new Tenant {
    Id = "acme",
    IsActive = false  // Immediately blocks access
});
```

### 4. Custom Domains
```csharp
// Allow acme.com → maps to acme tenant
new Tenant {
    Id = "acme",
    CustomDomain = "acme.com"
}
```

## Summary

✅ **No default tenant** - Tenant is always required
✅ **Auto-seeding** - Tenants created on docker-compose up
✅ **Database-driven** - All tenants stored in DynamoDB
✅ **Clear errors** - Users know immediately if tenant is missing/invalid
✅ **Production ready** - Strict validation prevents data issues

**"default" is now just another tenant, not a special fallback!**
