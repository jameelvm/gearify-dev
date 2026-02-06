#!/bin/bash

# DynamoDB Tables and Seed Data Initialization
# This script creates all DynamoDB tables and seeds initial data

set -e

CONFIG_DIR="/etc/localstack/init/ready.d"

echo "=========================================="
echo "Creating DynamoDB tables..."
echo "=========================================="

# Products table with GSI2-GSI6 for sorting
echo "  - Creating table: gearify-products"
awslocal dynamodb create-table \
  --table-name gearify-products \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
    AttributeName=GSI2PK,AttributeType=S \
    AttributeName=GSI2SK,AttributeType=S \
    AttributeName=GSI3PK,AttributeType=S \
    AttributeName=GSI3SK,AttributeType=S \
    AttributeName=GSI4PK,AttributeType=S \
    AttributeName=GSI4SK,AttributeType=S \
    AttributeName=GSI5PK,AttributeType=S \
    AttributeName=GSI5SK,AttributeType=S \
    AttributeName=GSI6PK,AttributeType=S \
    AttributeName=GSI6SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI2\",\"KeySchema\":[{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI3\",\"KeySchema\":[{\"AttributeName\":\"GSI3PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI3SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI4\",\"KeySchema\":[{\"AttributeName\":\"GSI4PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI4SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI5\",\"KeySchema\":[{\"AttributeName\":\"GSI5PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI5SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI6\",\"KeySchema\":[{\"AttributeName\":\"GSI6PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI6SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-products already exists, skipping..."

# Orders table
echo "  - Creating table: gearify-orders"
awslocal dynamodb create-table \
  --table-name gearify-orders \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-orders already exists, skipping..."

# Tenants table (PK/SK pattern for tenant service)
echo "  - Creating table: gearify-tenants"
awslocal dynamodb create-table \
  --table-name gearify-tenants \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-tenants already exists, skipping..."

# Feature flags table
echo "  - Creating table: gearify-feature-flags"
awslocal dynamodb create-table \
  --table-name gearify-feature-flags \
  --attribute-definitions \
    AttributeName=tenantId,AttributeType=S \
    AttributeName=flagKey,AttributeType=S \
  --key-schema \
    AttributeName=tenantId,KeyType=HASH \
    AttributeName=flagKey,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-feature-flags already exists, skipping..."

# Users table
echo "  - Creating table: gearify-users"
awslocal dynamodb create-table \
  --table-name gearify-users \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
    AttributeName=GSI2PK,AttributeType=S \
    AttributeName=GSI2SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI2\",\"KeySchema\":[{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-users already exists, skipping..."

# User Sessions table
echo "  - Creating table: UserSessions"
awslocal dynamodb create-table \
  --table-name UserSessions \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table UserSessions already exists, skipping..."

# MFA Codes table
echo "  - Creating table: MfaCodes"
awslocal dynamodb create-table \
  --table-name MfaCodes \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table MfaCodes already exists, skipping..."

# Catalog table
echo "  - Creating table: gearify-catalog"
awslocal dynamodb create-table \
  --table-name gearify-catalog \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
    AttributeName=GSI2PK,AttributeType=S \
    AttributeName=GSI2SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI2\",\"KeySchema\":[{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-catalog already exists, skipping..."

# Brands table
echo "  - Creating table: gearify-brands"
awslocal dynamodb create-table \
  --table-name gearify-brands \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-brands already exists, skipping..."

# Price Ranges table
echo "  - Creating table: gearify-price-ranges"
awslocal dynamodb create-table \
  --table-name gearify-price-ranges \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-price-ranges already exists, skipping..."

# Media table
echo "  - Creating table: gearify-media"
awslocal dynamodb create-table \
  --table-name gearify-media \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-media already exists, skipping..."

# Carts table
# PK: TENANT#{tenantId}#USER#{userId}
# SK: CART#METADATA (cart header) or ITEM#{productId} (cart items)
# GSI1: Find abandoned carts by status (GSI1PK: TENANT#{tenantId}#STATUS#{status}, GSI1SK: timestamp)
# GSI2: Find all carts for a user across tenants (GSI2PK: USER#{userId}, GSI2SK: TENANT#{tenantId})
echo "  - Creating table: gearify-carts"
awslocal dynamodb create-table \
  --table-name gearify-carts \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
    AttributeName=GSI2PK,AttributeType=S \
    AttributeName=GSI2SK,AttributeType=S \
  --key-schema \
    AttributeName=PK,KeyType=HASH \
    AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}},{\"IndexName\":\"GSI2\",\"KeySchema\":[{\"AttributeName\":\"GSI2PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI2SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"}}]" \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1 \
  2>/dev/null || echo "    Table gearify-carts already exists, skipping..."

echo "DynamoDB tables created successfully!"

# ==========================================
# DynamoDB Seed Data
# ==========================================
echo ""
echo "=========================================="
echo "Seeding DynamoDB data..."
echo "=========================================="

# Seed products for default-tenant (batch 1/2)
echo "  - Seeding products for default-tenant (batch 1/2)"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-default-tenant-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed default-tenant products batch 1"

# Seed products for default-tenant (batch 2/2)
echo "  - Seeding products for default-tenant (batch 2/2)"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-default-tenant-batch-2.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed default-tenant products batch 2"

# Seed products for test-tenant
echo "  - Seeding products for test-tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-test-tenant-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed test-tenant products"

# Seed products for acme-corp
echo "  - Seeding products for acme-corp"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-acme-corp-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed acme-corp products"

# Seed special collections products for testing
echo "  - Seeding special collections test products"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-special-collections.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed special collections products"

# Seed tenants
echo "  - Seeding tenants"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/tenants-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed tenants"

# Seed feature flags
echo "  - Seeding feature flags"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/feature-flags-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed feature flags"

# Seed brands for default tenant
echo "  - Seeding brands for default tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/brands-default-tenant.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed brands"

# Seed price ranges for default tenant
echo "  - Seeding price ranges for default tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/price-ranges-default-tenant.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed price ranges"

# Seed departments for default tenant
echo "  - Seeding departments for default tenant"
awslocal dynamodb batch-write-item \
  --request-items '{"gearify-catalog": [{"PutRequest": {"Item": {"PK": {"S": "TENANT#default#DEPARTMENT#a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"}, "SK": {"S": "METADATA"}, "EntityType": {"S": "DEPARTMENT"}, "GSI1PK": {"S": "TENANT#default#SLUG"}, "GSI1SK": {"S": "DEPARTMENT#cricket"}, "GSI2PK": {"S": "TENANT#default#DEPARTMENTS"}, "GSI2SK": {"S": "ORDER#0001"}, "Id": {"S": "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d"}, "TenantId": {"S": "default"}, "Name": {"S": "Cricket"}, "Slug": {"S": "cricket"}, "Description": {"S": "Cricket equipment and gear for all levels"}, "Icon": {"S": "cricket"}, "ImageUrl": {"S": ""}, "DisplayOrder": {"N": "1"}, "IsActive": {"BOOL": true}, "CreatedAt": {"S": "2025-12-25T00:00:00.000Z"}, "UpdatedAt": {"S": "2025-12-25T00:00:00.000Z"}, "CreatedBy": {"S": "system"}, "UpdatedBy": {"S": "system"}}}}]}' \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed departments"

# Seed catalog (categories, sections, subcategories) - split into 3 batches due to 25-item limit
echo "  - Seeding catalog for default tenant (batch 1/3)"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/catalog-default-tenant-batch-1.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed catalog batch 1"

echo "  - Seeding catalog for default tenant (batch 2/3)"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/catalog-default-tenant-batch-2.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed catalog batch 2"

echo "  - Seeding catalog for default tenant (batch 3/3)"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/catalog-default-tenant-batch-3.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed catalog batch 3"

# Seed special collections for default tenant
echo "  - Seeding special collections for default tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/special-collections-default-tenant.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed special collections"

# Seed sort options for default tenant
echo "  - Seeding sort options for default tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/sort-options-default-tenant.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed sort options"

echo "DynamoDB data seeded successfully!"
echo ""
