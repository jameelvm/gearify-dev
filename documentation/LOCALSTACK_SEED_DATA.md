# LocalStack Seed Data Configuration

## Overview

The Gearify platform uses LocalStack for local development, automatically seeding DynamoDB tables with test data when you run `docker-compose up`.

## Initialization Script

**Location:** `gearify-umbrella/scripts/localstack-init.sh`

This script automatically runs when LocalStack starts and:
1. Creates all necessary AWS resources (DynamoDB tables, S3 buckets, SQS queues, SNS topics)
2. Seeds DynamoDB with dummy data for multiple tenants
3. Creates demo users in Cognito

## Seeded Tenants

### 1. default-tenant (Free Plan)
**8 Products - Outdoor & Camping Gear**

| SKU | Name | Category | Price |
|-----|------|----------|-------|
| GR-CAM-001 | 4K Action Camera | Electronics | $299.99 |
| GR-TENT-002 | 3-Person Camping Tent | Camping | $149.99 |
| GR-BACK-003 | Hiking Backpack 40L | Camping | $89.99 |
| GR-BOOT-004 | Trekking Boots | Footwear | $179.99 |
| GR-DRONE-005 | GPS Drone with 4K Camera | Electronics | $599.99 |
| GR-SLEEP-006 | Sleeping Bag -10°C | Camping | $129.99 |
| GR-BIKE-007 | Mountain Bike 29" | Bikes | $1,299.99 |
| GR-WATCH-008 | GPS Sports Watch | Electronics | $349.99 |

### 2. test-tenant (Pro Plan)
**5 Products - Office & Electronics**

| SKU | Name | Category | Price |
|-----|------|----------|-------|
| TST-LAP-001 | Gaming Laptop 17" | Computers | $2,199.99 |
| TST-DESK-002 | Standing Desk | Furniture | $499.99 |
| TST-CHAIR-003 | Ergonomic Office Chair | Furniture | $349.99 |
| TST-PHONE-004 | Flagship Smartphone | Electronics | $999.99 |
| TST-HEAD-005 | Wireless Headphones | Audio | $299.99 |

### 3. acme-corp (Enterprise Plan)
**4 Products - Business Equipment**

| SKU | Name | Category | Price |
|-----|------|----------|-------|
| ACME-PROJ-001 | 4K Projector | Office | $1,899.99 |
| ACME-CONF-002 | Conference Phone | Office | $449.99 |
| ACME-PRINT-003 | Laser Printer | Office | $599.99 |
| ACME-SCAN-004 | Document Scanner | Office | $349.99 |

## Testing the API

### Get All Products for a Tenant

```bash
# Default tenant (8 camping/outdoor products)
curl -H "X-Tenant-Id: default-tenant" http://localhost:5001/api/catalog/products

# Test tenant (5 office/electronics products)
curl -H "X-Tenant-Id: test-tenant" http://localhost:5001/api/catalog/products

# Acme Corp (4 business products)
curl -H "X-Tenant-Id: acme-corp" http://localhost:5001/api/catalog/products
```

### Get Product by ID

```bash
curl -H "X-Tenant-Id: default-tenant" http://localhost:5001/api/catalog/products/prod-001
```

### Get Products by Category

```bash
# Get camping products for default-tenant
curl -H "X-Tenant-Id: default-tenant" "http://localhost:5001/api/catalog/products?category=Camping"

# Get electronics products
curl -H "X-Tenant-Id: default-tenant" "http://localhost:5001/api/catalog/products?category=Electronics"
```

## Using Swagger

1. Open Swagger UI: http://localhost:5001/swagger
2. Click on any endpoint (e.g., `GET /api/catalog/products`)
3. Click "Try it out"
4. Enter a tenant ID in the **X-Tenant-Id** field:
   - `default-tenant`
   - `test-tenant`
   - `acme-corp`
5. Click "Execute"

The X-Tenant-Id header is **required** for all API endpoints except `/health` and `/swagger`.

## DynamoDB Table Schema

**Table Name:** `gearify-products`

**Primary Key:**
- **PK** (Partition Key): `TENANT#{tenantId}`
- **SK** (Sort Key): `PRODUCT#{productId}`

**Global Secondary Index (GSI1):**
- **GSI1PK**: `TENANT#{tenantId}#CATEGORY#{category}`
- **GSI1SK**: `PRODUCT#{productId}`

This single-table design allows efficient queries by:
- Tenant (get all products for a tenant)
- Tenant + Category (get products by category within a tenant)
- Tenant + Product ID (get specific product)

## Reinitializing Data

If you need to reset the data:

```bash
# Delete the table
docker exec gearify-localstack awslocal dynamodb delete-table --table-name gearify-products

# Rerun the init script
docker exec gearify-localstack sh /etc/localstack/init/ready.d/init-aws.sh
```

## Adding More Test Data

To add more products, edit `gearify-umbrella/scripts/localstack-init.sh` and add more `insert_product` calls:

```bash
insert_product "tenant-id" "product-id" "SKU" "Product Name" "Description" "Category" "Brand" "Price" "ComparePrice" "IsActive"
```

Example:
```bash
insert_product "default-tenant" "prod-009" "GR-KAYAK-009" "Inflatable Kayak" "2-person inflatable kayak" "Water Sports" "Gearify" "399.99" "499.99" "true"
```

## Demo Users (Cognito)

The init script also creates demo Cognito users:

| Email | Password | Purpose |
|-------|----------|---------|
| admin@gearify.com | Admin123! | Admin user |
| user@global-demo.com | User123! | Regular user |

These can be used for authentication testing when implementing the auth service.

## LocalStack Persistence

Data persists between container restarts because LocalStack is configured with:
- `PERSISTENCE=1` in docker-compose.yml
- Volume mount: `localstack-data:/var/lib/localstack`

To completely reset LocalStack:
```bash
docker-compose down -v
docker-compose up
```

## Troubleshooting

### Products not showing up

1. Check LocalStack is running:
   ```bash
   docker ps | grep localstack
   ```

2. Check table exists:
   ```bash
   docker exec gearify-localstack awslocal dynamodb list-tables
   ```

3. Check table schema:
   ```bash
   docker exec gearify-localstack awslocal dynamodb describe-table --table-name gearify-products
   ```

4. Check products in table:
   ```bash
   docker exec gearify-localstack awslocal dynamodb scan --table-name gearify-products --limit 5
   ```

### AWS Credentials Error

If you see "Unable to get IAM security credentials", ensure your service code uses fake credentials for LocalStack:

```csharp
var credentials = new BasicAWSCredentials("test", "test");
var dynamoConfig = new AmazonDynamoDBConfig { ServiceURL = "http://localstack:4566" };
var client = new AmazonDynamoDBClient(credentials, dynamoConfig);
```

## Next Steps

- Add more diverse product data
- Add inventory data
- Add order history
- Add customer reviews
- Configure S3 seed data for product images
