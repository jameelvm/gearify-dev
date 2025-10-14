# Customization Guide

## Theming

### Updating Tenant Themes
Edit `scripts/seed/tenants.json`:

```json
{
  "tenantId": "my-tenant",
  "name": "My Shop",
  "domain": "myshop.localhost",
  "theme": {
    "primaryColor": "#ff5722",
    "secondaryColor": "#03a9f4",
    "logoUrl": "https://example.com/logo.png",
    "fontFamily": "Poppins, sans-serif"
  }
}
```

Then re-seed:
```bash
make seed-clean
```

### Dynamic Theme Loading
Frontend reads theme from Tenant Service API and injects CSS variables:

```css
:root {
  --primary-color: #1976d2;
  --secondary-color: #dc004e;
}
```

## Feature Flags

### DynamoDB Feature Flags
Stored in `gearify-feature-flags` table:
- Key: `tenantId` + `flagKey`
- Value: `enabled` (boolean)

### Toggle Features Per Tenant
```bash
# Enable PayPal for specific tenant
awslocal dynamodb put-item \
  --table-name gearify-feature-flags \
  --item '{"tenantId": {"S": "my-tenant"}, "flagKey": {"S": "enable-paypal"}, "enabled": {"BOOL": true}}'
```

### Redis Feature Toggles
Quick toggles in Redis (faster reads):
```bash
docker exec gearify-redis redis-cli SET "feature:my-tenant:enable-checkout" "true"
```

## Tenant Switching

### Local Testing
Use Host header or subdomain:

```bash
# Default tenant
curl http://localhost:8080/api/catalog/products

# Demo tenant
curl http://localhost:8080/api/catalog/products -H "Host: demo.localhost"
```

### JWT Claims
Services extract `tenantId` from JWT token claims for automatic isolation.

## Environment Overrides

### Per-Service Configuration
Create `.env.catalog` for catalog-specific vars:
```bash
CACHE_TTL=3600
MAX_RESULTS=100
```

Reference in docker-compose:
```yaml
catalog-svc:
  env_file:
    - .env
    - .env.catalog
```

## Adding New Tenants

See [TENANT-SETUP.md](TENANT-SETUP.md) for detailed guide.
