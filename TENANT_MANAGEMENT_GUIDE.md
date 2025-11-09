# Tenant Management Guide

## Overview

Tenants are now stored in **DynamoDB** (`gearify-tenants` table) instead of being hardcoded. This allows dynamic tenant management through API endpoints.

## Quick Start

### 1. Initialize Tenant Table

Run the initialization script to create the table and seed default tenants:

```bash
cd scripts
bash init-tenant-table.sh
```

This creates:
- **default** - Default tenant (Free plan, 10 users)
- **acme** - Acme Corporation (Pro plan, 50 users)
- **contoso** - Contoso Ltd (Enterprise plan, 200 users)
- **fabrikam** - Fabrikam Inc (Pro plan, 100 users)
- **demo** - Demo tenant (Free plan, 5 users)

### 2. Verify Tenants

```bash
aws dynamodb scan \
  --table-name gearify-tenants \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

### 3. Test Validation

```bash
# Valid tenant - should return 200
curl http://localhost:8080/api/tenants/validate/acme

# Invalid tenant - should return 404
curl http://localhost:8080/api/tenants/validate/invalid
```

## DynamoDB Table Schema

**Table Name:** `gearify-tenants`

**Keys:**
- **PK** (Partition Key): `TENANT#{tenantId}`
- **SK** (Sort Key): `TENANT#{tenantId}`

**Attributes:**
```json
{
  "PK": "TENANT#acme",
  "SK": "TENANT#acme",
  "Id": "acme",
  "Name": "Acme Corporation",
  "IsActive": true,
  "CreatedAt": "2025-01-01T00:00:00.000Z",
  "UpdatedAt": "2025-01-01T00:00:00.000Z",
  "Plan": "Pro",
  "MaxUsers": 50,
  "ContactEmail": "admin@acme.com",
  "CustomDomain": "acme.com"  // optional
}
```

## API Endpoints

### Public Endpoints (No Auth Required)

#### Validate Tenant
```http
GET /api/tenants/validate/{tenantId}
```

**Response (200 OK):**
```json
{
  "tenantId": "acme",
  "isValid": true,
  "isActive": true
}
```

**Response (404 Not Found):**
```json
{
  "error": "Tenant not found",
  "message": "The tenant 'xyz' does not exist or is not active.",
  "tenantId": "xyz"
}
```

#### List Active Tenants
```http
GET /api/tenants/list
```

**Response:**
```json
["acme", "contoso", "default", "demo", "fabrikam"]
```

### Management Endpoints (Admin Only)

#### Get All Tenants
```http
GET /api/tenants?activeOnly=true
```

**Response:**
```json
[
  {
    "id": "acme",
    "name": "Acme Corporation",
    "isActive": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z",
    "plan": "Pro",
    "maxUsers": 50,
    "contactEmail": "admin@acme.com",
    "customDomain": null
  }
]
```

#### Get Tenant by ID
```http
GET /api/tenants/{tenantId}
```

#### Create Tenant
```http
POST /api/tenants
Content-Type: application/json

{
  "id": "newcorp",
  "name": "New Corporation",
  "plan": "Enterprise",
  "maxUsers": 500,
  "contactEmail": "admin@newcorp.com",
  "customDomain": "newcorp.com"
}
```

**Tenant ID Rules:**
- Lowercase alphanumeric with hyphens only
- Must start and end with alphanumeric
- 3-63 characters
- Examples: `acme`, `acme-corp`, `my-company-123`

#### Update Tenant
```http
PUT /api/tenants/{tenantId}
Content-Type: application/json

{
  "name": "Updated Name",
  "isActive": true,
  "plan": "Enterprise",
  "maxUsers": 1000
}
```

#### Delete Tenant (Soft Delete)
```http
DELETE /api/tenants/{tenantId}
```

Soft deletes the tenant by setting `IsActive = false`. Data is preserved.

## Usage Examples

### Create a New Tenant via API

```bash
curl -X POST http://localhost:8080/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "id": "techcorp",
    "name": "Tech Corporation",
    "plan": "Pro",
    "maxUsers": 100,
    "contactEmail": "admin@techcorp.com"
  }'
```

### Access the New Tenant

```
http://techcorp.localhost.direct:4200
```

### Deactivate a Tenant

```bash
curl -X PUT http://localhost:8080/api/tenants/techcorp \
  -H "Content-Type: application/json" \
  -d '{"isActive": false}'
```

Now `http://techcorp.localhost.direct:4200` will show the "Tenant Not Found" page.

### Reactivate a Tenant

```bash
curl -X PUT http://localhost:8080/api/tenants/techcorp \
  -H "Content-Type: application/json" \
  -d '{"isActive": true}'
```

## Manual DynamoDB Operations

### Create Tenant Manually

```bash
aws dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "PK": {"S": "TENANT#mycorp"},
    "SK": {"S": "TENANT#mycorp"},
    "Id": {"S": "mycorp"},
    "Name": {"S": "My Corporation"},
    "IsActive": {"BOOL": true},
    "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "Plan": {"S": "Free"},
    "MaxUsers": {"N": "10"},
    "ContactEmail": {"S": "admin@mycorp.com"}
  }' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

### Query Tenant

```bash
aws dynamodb get-item \
  --table-name gearify-tenants \
  --key '{"PK": {"S": "TENANT#acme"}, "SK": {"S": "TENANT#acme"}}' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

### List All Tenants

```bash
aws dynamodb scan \
  --table-name gearify-tenants \
  --endpoint-url http://localhost:4566 \
  --region us-east-1 \
  --output table
```

### Delete Tenant (Hard Delete)

```bash
aws dynamodb delete-item \
  --table-name gearify-tenants \
  --key '{"PK": {"S": "TENANT#demo"}, "SK": {"S": "TENANT#demo"}}' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

## Repository Interface

The `ITenantRepository` provides:

```csharp
Task<Tenant?> GetByIdAsync(string tenantId);
Task<IEnumerable<Tenant>> GetAllActiveAsync();
Task<IEnumerable<Tenant>> GetAllAsync();
Task CreateAsync(Tenant tenant);
Task UpdateAsync(Tenant tenant);
Task DeleteAsync(string tenantId);  // Soft delete
Task<bool> ExistsAndActiveAsync(string tenantId);
```

## Flow Diagram

```
User accesses: http://acme.localhost.direct:4200
    ↓
Angular APP_INITIALIZER
    ↓
GET /api/tenants/validate/acme
    ↓
TenantValidationController
    ↓
ITenantRepository.ExistsAndActiveAsync("acme")
    ↓
DynamoDB Query: PK=TENANT#acme, SK=TENANT#acme
    ↓
✅ Found & Active → Allow access
❌ Not found or Inactive → Redirect to /tenant-not-found
```

## Production Considerations

### 1. Add Authentication to Management Endpoints

```csharp
[Authorize(Roles = "SuperAdmin")]
public class TenantsController : ControllerBase
```

### 2. Add Caching

```csharp
// Cache tenant validation for 5 minutes
services.AddDistributedMemoryCache();
```

### 3. Add Rate Limiting

Prevent abuse of validation endpoint.

### 4. Remove Test Endpoint

Remove or secure the `/api/tenants/list` endpoint in production.

### 5. Add Audit Logging

Log all tenant creation/modification operations.

### 6. Implement Tenant Onboarding Workflow

Instead of direct API creation, implement proper onboarding:
- Registration form
- Email verification
- Payment processing
- Automated tenant provisioning

## Summary

✅ **Tenants stored in DynamoDB** - No more hardcoded values
✅ **Dynamic validation** - Real-time checks against database
✅ **Full CRUD API** - Create, read, update, delete tenants
✅ **Soft deletes** - Preserve data when deactivating
✅ **Easy seeding** - One script to initialize defaults
✅ **Production ready** - Scalable architecture

Your multi-tenant system is now fully database-driven!
