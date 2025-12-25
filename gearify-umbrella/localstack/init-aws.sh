#!/bin/bash

set -e

echo "=========================================="
echo "Initializing LocalStack AWS Resources"
echo "=========================================="

# Configuration
LOCALSTACK_ENDPOINT="http://localhost:4566"
CONFIG_DIR="/etc/localstack/init/ready.d"

# Wait for LocalStack to be ready
echo "Waiting for LocalStack to be ready..."
until awslocal s3 ls 2>/dev/null; do
  echo "Waiting for LocalStack..."
  sleep 2
done
echo "LocalStack is ready!"

# ==========================================
# DynamoDB Tables
# ==========================================
echo ""
echo "Creating DynamoDB tables..."

# Products table
echo "  - Creating table: gearify-products"
awslocal dynamodb create-table \
  --table-name gearify-products \
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

echo "DynamoDB tables created successfully!"

# ==========================================
# DynamoDB Seed Data
# ==========================================
echo ""
echo "Seeding DynamoDB data..."

# Seed products for default-tenant
echo "  - Seeding products for default-tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/products-default-tenant-batch.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed default-tenant products"

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

echo "DynamoDB data seeded successfully!"

# ==========================================
# S3 Buckets
# ==========================================
echo ""
echo "Creating S3 buckets..."

# Product images bucket
echo "  - Creating bucket: gearify-product-images"
awslocal s3 mb s3://gearify-product-images --region us-east-1 2>/dev/null || echo "    Bucket already exists"
awslocal s3api put-bucket-cors \
  --bucket gearify-product-images \
  --cors-configuration file://${CONFIG_DIR}/s3/cors/product-images-cors.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to set CORS"

# Tenant assets bucket
echo "  - Creating bucket: gearify-tenant-assets"
awslocal s3 mb s3://gearify-tenant-assets --region us-east-1 2>/dev/null || echo "    Bucket already exists"
awslocal s3api put-bucket-cors \
  --bucket gearify-tenant-assets \
  --cors-configuration file://${CONFIG_DIR}/s3/cors/tenant-assets-cors.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to set CORS"

# Order documents bucket
echo "  - Creating bucket: gearify-order-documents"
awslocal s3 mb s3://gearify-order-documents --region us-east-1 2>/dev/null || echo "    Bucket already exists"

echo "S3 buckets created successfully!"

# ==========================================
# SQS Queues
# ==========================================
echo ""
echo "Creating SQS queues..."

awslocal sqs create-queue --queue-name gearify-order-created --region us-east-1 2>/dev/null || echo "  - Queue gearify-order-created already exists"
awslocal sqs create-queue --queue-name gearify-payment-processed --region us-east-1 2>/dev/null || echo "  - Queue gearify-payment-processed already exists"
awslocal sqs create-queue --queue-name gearify-inventory-updated --region us-east-1 2>/dev/null || echo "  - Queue gearify-inventory-updated already exists"
awslocal sqs create-queue --queue-name gearify-notification-requested --region us-east-1 2>/dev/null || echo "  - Queue gearify-notification-requested already exists"
awslocal sqs create-queue --queue-name gearify-shipping-requested --region us-east-1 2>/dev/null || echo "  - Queue gearify-shipping-requested already exists"

echo "SQS queues created successfully!"

# ==========================================
# SNS Topics
# ==========================================
echo ""
echo "Creating SNS topics..."

ORDER_TOPIC_ARN=$(awslocal sns create-topic --name gearify-order-events --region us-east-1 --output text 2>/dev/null || echo "")
PAYMENT_TOPIC_ARN=$(awslocal sns create-topic --name gearify-payment-events --region us-east-1 --output text 2>/dev/null || echo "")
INVENTORY_TOPIC_ARN=$(awslocal sns create-topic --name gearify-inventory-events --region us-east-1 --output text 2>/dev/null || echo "")

echo "  - Created topic: gearify-order-events"
echo "  - Created topic: gearify-payment-events"
echo "  - Created topic: gearify-inventory-events"

echo "SNS topics created successfully!"

# ==========================================
# Secrets Manager
# ==========================================
echo ""
echo "Creating Secrets Manager secrets..."

awslocal secretsmanager create-secret \
  --name gearify/payment/stripe-api-key \
  --description "Stripe payment gateway API key for development" \
  --secret-string '{"apiKey":"sk_test_dev_12345678901234567890","publishableKey":"pk_test_dev_12345678901234567890"}' \
  --region us-east-1 \
  2>/dev/null || echo "  - Secret gearify/payment/stripe-api-key already exists"

awslocal secretsmanager create-secret \
  --name gearify/auth/jwt-signing-key \
  --description "JWT signing key for authentication tokens" \
  --secret-string '{"key":"dev-super-secret-jwt-signing-key-2025-gearify","algorithm":"HS256","expirationMinutes":60}' \
  --region us-east-1 \
  2>/dev/null || echo "  - Secret gearify/auth/jwt-signing-key already exists"

awslocal secretsmanager create-secret \
  --name gearify/notification/smtp-credentials \
  --description "SMTP credentials for sending emails" \
  --secret-string '{"host":"localhost","port":1025,"username":"dev@gearify.com","password":"dev-password","from":"noreply@gearify.com"}' \
  --region us-east-1 \
  2>/dev/null || echo "  - Secret gearify/notification/smtp-credentials already exists"

echo "Secrets Manager secrets created successfully!"

# ==========================================
# SSM Parameter Store
# ==========================================
echo ""
echo "Creating SSM parameters..."

awslocal ssm put-parameter \
  --name "/gearify/config/feature-flags" \
  --description "Global feature flags configuration" \
  --value '{"enableNewCheckout":false,"enableProductReviews":true,"enableWishlist":true,"enableLiveChat":false,"maintenanceMode":false}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "  - Parameter /gearify/config/feature-flags already exists"

awslocal ssm put-parameter \
  --name "/gearify/config/api-rate-limits" \
  --description "API rate limiting configuration" \
  --value '{"requestsPerMinute":100,"requestsPerHour":5000,"burstSize":20,"enableRateLimiting":true}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "  - Parameter /gearify/config/api-rate-limits already exists"

awslocal ssm put-parameter \
  --name "/gearify/config/cache-settings" \
  --description "Caching configuration for services" \
  --value '{"productCacheTTL":300,"categoryCacheTTL":600,"userSessionTTL":3600,"enableDistributedCache":true,"cacheProvider":"Redis"}' \
  --type String \
  --region us-east-1 \
  2>/dev/null || echo "  - Parameter /gearify/config/cache-settings already exists"

echo "SSM parameters created successfully!"

# ==========================================
# Summary
# ==========================================
echo ""
echo "=========================================="
echo "LocalStack initialization completed!"
echo "=========================================="
echo ""
echo "Resources created:"
echo "  - DynamoDB Tables: $(awslocal dynamodb list-tables --region us-east-1 --output text | wc -w)"
echo "  - S3 Buckets: $(awslocal s3 ls | wc -l)"
echo "  - SQS Queues: 5"
echo "  - SNS Topics: 3"
echo "  - Secrets: 3"
echo "  - SSM Parameters: 3"
echo ""
echo "Ready for development!"
echo "=========================================="
