# Quick Start: Multi-Tenancy with Auto-Seeding

## TL;DR

```bash
# 1. Start everything
cd gearify-umbrella
docker-compose up -d

# 2. Wait for auto-seeding (check logs)
docker logs -f gearify-localstack | grep "tenant"

# 3. Access your tenant
open http://acme.localhost.direct:4200

# Done! ✅
```

## What You Get Out of the Box

**5 Pre-Seeded Tenants:**
- `default` - http://default.localhost.direct:4200
- `acme` - http://acme.localhost.direct:4200
- `contoso` - http://contoso.localhost.direct:4200
- `fabrikam` - http://fabrikam.localhost.direct:4200
- `demo` - http://demo.localhost.direct:4200

## Important Rules

### ✅ This Works
```
http://acme.localhost.direct:4200        ← Valid tenant subdomain
curl -H "X-Tenant-Id: acme" <api-url>   ← Valid tenant header
```

### ❌ This Fails
```
http://localhost:4200                    ← No tenant (shows error)
http://xyz.localhost.direct:4200         ← Tenant not in DB (shows error)
```

## Quick Commands

### List All Tenants
```bash
curl http://localhost:8080/api/tenants/list
```

### Create New Tenant
```bash
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "id": "newcorp",
    "name": "New Corp",
    "plan": "Pro",
    "maxUsers": 50,
    "contactEmail": "admin@newcorp.com"
  }'

# Then access:
open http://newcorp.localhost.direct:4200
```

### Validate Tenant
```bash
curl http://localhost:8080/api/tenants/validate/acme
```

### Re-Seed Tenants
```bash
docker-compose exec localstack bash /etc/localstack/init/ready.d/seed-tenants.sh
```

## Architecture

```
User → http://acme.localhost.direct:4200
         ↓
    Angular App (detects "acme" from subdomain)
         ↓
    Validates: GET /api/tenants/validate/acme
         ↓
    DynamoDB: Query TENANT#acme
         ↓
    ✅ Valid → App loads
    ❌ Invalid → Error page
```

## Files Changed

**Auto-Seeding:**
- `gearify-umbrella/scripts/seed-tenants.sh` - Seed script
- `gearify-umbrella/docker-compose.yml` - Mounts seed script

**Tenant Validation:**
- `gearify-tenant-svc/Infrastructure/Repositories/DynamoDbTenantRepository.cs` - DynamoDB repo
- `gearify-tenant-svc/API/Controllers/TenantValidationController.cs` - Validation endpoint
- `gearify-tenant-svc/API/Controllers/TenantsController.cs` - CRUD endpoints

**Frontend:**
- `gearify-web/src/app/app.config.ts` - App initializer validates tenant
- `gearify-web/src/app/core/interceptors/auth.interceptor.ts` - Extracts tenant from subdomain
- `gearify-web/src/app/core/services/tenant.service.ts` - Validation service
- `gearify-web/src/app/features/errors/tenant-not-found.component.ts` - Error page

**API Gateway:**
- `gearify-api-gateway/Middleware/TenantResolutionMiddleware.cs` - Extracts tenant

## Troubleshooting

### "Tenant not found" error on valid tenant
```bash
# Check if tenants were seeded
docker logs gearify-localstack | grep "Seeding"

# Manually re-seed
docker-compose exec localstack bash /etc/localstack/init/ready.d/seed-tenants.sh
```

### Can't access localhost:4200
**This is expected!** You must use a subdomain:
- ❌ `http://localhost:4200`
- ✅ `http://default.localhost.direct:4200`

### Tenant validation fails
```bash
# Check tenant exists
aws dynamodb get-item \
  --table-name gearify-tenants \
  --key '{"PK": {"S": "TENANT#acme"}, "SK": {"S": "TENANT#acme"}}' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

## Next Steps

- See `TENANT_MANAGEMENT_GUIDE.md` for full API documentation
- See `NO_DEFAULT_TENANT_GUIDE.md` for architecture details
- See `SUBDOMAIN_TENANCY_SETUP.md` for production setup

**You're all set! Start building your multi-tenant SaaS! 🚀**
