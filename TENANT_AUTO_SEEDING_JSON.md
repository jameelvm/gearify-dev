# Tenant Auto-Seeding via JSON Files

## Overview

Tenants are now automatically seeded from **JSON files** in the `localstack/dynamodb/data` directory, following the same pattern as products, orders, and other data.

## How It Works

```
docker-compose up
    ↓
LocalStack container starts
    ↓
Runs: /etc/localstack/init/ready.d/init-aws.sh
    ↓
Creates table from: localstack/init-aws.sh (line 64-75)
    ↓
Seeds data from: localstack/dynamodb/data/tenants-batch.json
    ↓
5 tenants loaded into gearify-tenants table ✅
```

## File Structure

```
gearify-umbrella/
├── localstack/
│   ├── init-aws.sh                         # Main init script
│   └── dynamodb/
│       ├── tables/
│       │   └── tenants.json                # Table schema (not used, kept for reference)
│       └── data/
│           ├── tenants.json                # Human-readable tenant data
│           └── tenants-batch.json          # Batch write format (actually used)
```

## Table Schema

**File:** `localstack/dynamodb/tables/tenants.json`

```json
{
  "TableName": "gearify-tenants",
  "AttributeDefinitions": [
    { "AttributeName": "PK", "AttributeType": "S" },
    { "AttributeName": "SK", "AttributeType": "S" }
  ],
  "KeySchema": [
    { "AttributeName": "PK", "KeyType": "HASH" },
    { "AttributeName": "SK", "KeyType": "RANGE" }
  ],
  "BillingMode": "PAY_PER_REQUEST"
}
```

**Note:** This file is for reference only. The actual table is created by `init-aws.sh` (line 64-75).

## Seed Data Format

### Human-Readable Format
**File:** `localstack/dynamodb/data/tenants.json`

```json
[
  {
    "PK": {"S": "TENANT#default"},
    "SK": {"S": "TENANT#default"},
    "Id": {"S": "default"},
    "Name": {"S": "Default Organization"},
    "IsActive": {"BOOL": true},
    "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "Plan": {"S": "Free"},
    "MaxUsers": {"N": "10"},
    "ContactEmail": {"S": "admin@default.local"}
  }
]
```

### Batch Write Format (Used for Loading)
**File:** `localstack/dynamodb/data/tenants-batch.json`

```json
{
  "gearify-tenants": [
    {
      "PutRequest": {
        "Item": {
          "PK": {"S": "TENANT#default"},
          "SK": {"S": "TENANT#default"},
          "Id": {"S": "default"},
          "Name": {"S": "Default Organization"},
          "IsActive": {"BOOL": true},
          "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
          "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
          "Plan": {"S": "Free"},
          "MaxUsers": {"N": "10"},
          "ContactEmail": {"S": "admin@default.local"}
        }
      }
    }
  ]
}
```

## Pre-Seeded Tenants

| Tenant ID | Name | Plan | Max Users | Contact Email |
|-----------|------|------|-----------|---------------|
| `default` | Default Organization | Free | 10 | admin@default.local |
| `acme` | Acme Corporation | Pro | 50 | admin@acme.com |
| `contoso` | Contoso Ltd | Enterprise | 200 | admin@contoso.com |
| `fabrikam` | Fabrikam Inc | Pro | 100 | admin@fabrikam.com |
| `demo` | Demo Tenant | Free | 5 | demo@gearify.com |

## Adding New Tenants

### Option 1: Edit JSON Files (Before First Run)

**1. Add to `tenants.json`:**
```json
{
  "PK": {"S": "TENANT#newcorp"},
  "SK": {"S": "TENANT#newcorp"},
  "Id": {"S": "newcorp"},
  "Name": {"S": "New Corporation"},
  "IsActive": {"BOOL": true},
  "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
  "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
  "Plan": {"S": "Pro"},
  "MaxUsers": {"N": "100"},
  "ContactEmail": {"S": "admin@newcorp.com"}
}
```

**2. Add to `tenants-batch.json`:**
```json
{
  "PutRequest": {
    "Item": {
      "PK": {"S": "TENANT#newcorp"},
      "SK": {"S": "TENANT#newcorp"},
      "Id": {"S": "newcorp"},
      "Name": {"S": "New Corporation"},
      "IsActive": {"BOOL": true},
      "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
      "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
      "Plan": {"S": "Pro"},
      "MaxUsers": {"N": "100"},
      "ContactEmail": {"S": "admin@newcorp.com"}
    }
  }
}
```

**3. Restart LocalStack:**
```bash
docker-compose restart localstack
```

### Option 2: Use API (After Running)

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
```

### Option 3: Direct DynamoDB Insert

```bash
aws dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "PK": {"S": "TENANT#newcorp"},
    "SK": {"S": "TENANT#newcorp"},
    "Id": {"S": "newcorp"},
    "Name": {"S": "New Corporation"},
    "IsActive": {"BOOL": true},
    "CreatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "UpdatedAt": {"S": "2025-01-08T00:00:00.000Z"},
    "Plan": {"S": "Pro"},
    "MaxUsers": {"N": "100"},
    "ContactEmail": {"S": "admin@newcorp.com"}
  }' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

## Verification

### Check Loaded Tenants
```bash
# List all tenants
curl http://localhost:8080/api/tenants/list

# Validate specific tenant
curl http://localhost:8080/api/tenants/validate/acme

# Get tenant details
curl http://localhost:8080/api/tenants/acme
```

### Check DynamoDB Directly
```bash
# Scan all tenants
aws dynamodb scan \
  --table-name gearify-tenants \
  --endpoint-url http://localhost:4566 \
  --region us-east-1

# Get specific tenant
aws dynamodb get-item \
  --table-name gearify-tenants \
  --key '{"PK": {"S": "TENANT#acme"}, "SK": {"S": "TENANT#acme"}}' \
  --endpoint-url http://localhost:4566 \
  --region us-east-1
```

## Troubleshooting

### Tenants Not Loading

**Check init script ran:**
```bash
docker logs gearify-localstack | grep -A 5 "Seeding tenants"
```

Should show:
```
  - Seeding tenants
DynamoDB data seeded successfully!
```

**Manually re-run seeding:**
```bash
docker exec gearify-localstack awslocal dynamodb batch-write-item \
  --request-items file:///etc/localstack/init/ready.d/dynamodb/data/tenants-batch.json \
  --region us-east-1
```

### Table Schema Mismatch

If you see errors about key schema, the old table might exist:

```bash
# Delete old table
aws dynamodb delete-table \
  --table-name gearify-tenants \
  --endpoint-url http://localhost:4566 \
  --region us-east-1

# Restart LocalStack to recreate
docker-compose restart localstack
```

### Data Not Persisting

LocalStack persistence is enabled, but if you want fresh data:

```bash
# Clear LocalStack data
docker-compose down
docker volume rm gearify-umbrella_localstack-data

# Start fresh
docker-compose up -d
```

## Comparison: JSON vs Bash Scripts

### ❌ Old Approach (Bash Scripts)
```bash
# Hardcoded AWS CLI commands
aws dynamodb put-item --table-name gearify-tenants --item '{...}'
```

**Problems:**
- Verbose bash commands
- Hard to read/maintain
- Mixed with infrastructure code
- Difficult to version control data

### ✅ New Approach (JSON Files)
```json
{
  "gearify-tenants": [
    {"PutRequest": {"Item": {...}}}
  ]
}
```

**Benefits:**
- ✅ Clean separation: schema vs data vs infrastructure
- ✅ Easy to edit/review
- ✅ Git-friendly diffs
- ✅ Consistent with other seed data (products, orders)
- ✅ IDE syntax highlighting & validation
- ✅ Reusable across environments

## Files Modified

| File | Purpose | Status |
|------|---------|--------|
| `localstack/init-aws.sh` | Updated table creation (line 64-75) | ✅ Updated |
| `localstack/dynamodb/tables/tenants.json` | Table schema reference | ✅ Updated (PK/SK) |
| `localstack/dynamodb/data/tenants.json` | Human-readable seed data | ✅ Updated |
| `localstack/dynamodb/data/tenants-batch.json` | Batch write format | ✅ Updated |
| `scripts/seed-tenants.sh` | Old bash script | ❌ Deleted |
| `docker-compose.yml` | Removed bash script mount | ✅ Updated |

## Summary

✅ **Tenants auto-seed from JSON** - Just like products and orders
✅ **No bash scripts needed** - Clean, declarative data files
✅ **Easy to add tenants** - Edit JSON, restart LocalStack
✅ **Consistent with existing patterns** - Follows your project's conventions
✅ **Git-friendly** - Easy to see data changes in PRs

**Your tenant seeding is now fully JSON-driven!** 🎉
