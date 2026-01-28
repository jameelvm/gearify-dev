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
echo "Seeding DynamoDB data..."

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

# ==========================================
# Checkout Flow - Dead Letter Queues (DLQs)
# ==========================================
echo "  - Creating checkout DLQs..."

# Create DLQs first (they need to exist before main queues reference them)
awslocal sqs create-queue \
  --queue-name gearify-checkout-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-checkout-events-dlq already exists"

awslocal sqs create-queue \
  --queue-name gearify-order-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-events-dlq already exists"

awslocal sqs create-queue \
  --queue-name gearify-payment-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-payment-events-dlq already exists"

awslocal sqs create-queue \
  --queue-name gearify-shipping-events-dlq \
  --attributes '{"MessageRetentionPeriod":"1209600"}' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-events-dlq already exists"

# Get DLQ ARNs for redrive policy
CHECKOUT_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-checkout-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
ORDER_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
PAYMENT_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-payment-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
SHIPPING_DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-events-dlq --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")

# ==========================================
# Checkout Flow - Main Event Queues
# ==========================================
echo "  - Creating checkout main queues..."

# Checkout initiated queue (order-svc listens)
awslocal sqs create-queue \
  --queue-name gearify-checkout-initiated-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$CHECKOUT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-checkout-initiated-queue already exists"

# Order created queue (payment-svc listens)
awslocal sqs create-queue \
  --queue-name gearify-order-created-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$ORDER_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-order-created-queue already exists"

# Order Service payment events queue (order-svc listens for payment outcomes)
awslocal sqs create-queue \
  --queue-name order-payment-events-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue order-payment-events-queue already exists"

# Payment failed queue (order-svc listens for saga rollback)
awslocal sqs create-queue \
  --queue-name gearify-payment-failed-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-payment-failed-queue already exists"

# Shipping created queue (order-svc listens)
awslocal sqs create-queue \
  --queue-name gearify-shipping-created-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$SHIPPING_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-created-queue already exists"

# Shipping status update queue (order-svc listens)
awslocal sqs create-queue \
  --queue-name gearify-shipping-status-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$SHIPPING_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-shipping-status-queue already exists"

# Notification Service payment events queue (notification-svc listens for payment emails)
awslocal sqs create-queue \
  --queue-name notification-payment-events-queue \
  --attributes "{
    \"VisibilityTimeout\":\"300\",
    \"MessageRetentionPeriod\":\"1209600\",
    \"ReceiveMessageWaitTimeSeconds\":\"20\",
    \"RedrivePolicy\":\"{\\\"deadLetterTargetArn\\\":\\\"$PAYMENT_DLQ_ARN\\\",\\\"maxReceiveCount\\\":3}\"
  }" \
  --region us-east-1 \
  2>/dev/null || echo "    Queue notification-payment-events-queue already exists"

# Search Service queue for catalog events
echo "  - Creating queue: search-catalog-events-queue"
awslocal sqs create-queue \
  --queue-name search-catalog-events-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue search-catalog-events-queue already exists"

# Image processing queue with attributes
echo "  - Creating queue: gearify-image-processing-queue"
awslocal sqs create-queue \
  --queue-name gearify-image-processing-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-image-processing-queue already exists"

# Product thumbnail update queue with attributes
echo "  - Creating queue: gearify-product-thumbnail-update-queue"
awslocal sqs create-queue \
  --queue-name gearify-product-thumbnail-update-queue \
  --attributes '{
    "VisibilityTimeout":"300",
    "MessageRetentionPeriod":"1209600",
    "ReceiveMessageWaitTimeSeconds":"20"
  }' \
  --region us-east-1 \
  2>/dev/null || echo "    Queue gearify-product-thumbnail-update-queue already exists"

echo "SQS queues created successfully!"

# ==========================================
# SNS Topics
# ==========================================
echo ""
echo "Creating SNS topics..."

ORDER_TOPIC_ARN=$(awslocal sns create-topic --name gearify-order-events --region us-east-1 --output text 2>/dev/null || echo "")
PAYMENT_TOPIC_ARN=$(awslocal sns create-topic --name gearify-payment-events --region us-east-1 --output text 2>/dev/null || echo "")
INVENTORY_TOPIC_ARN=$(awslocal sns create-topic --name gearify-inventory-events --region us-east-1 --output text 2>/dev/null || echo "")
MEDIA_TOPIC_ARN=$(awslocal sns create-topic --name gearify-media-upload-events --region us-east-1 --output text 2>/dev/null || echo "")
IMAGE_PROCESSING_COMPLETED_TOPIC_ARN=$(awslocal sns create-topic --name gearify-image-processing-completed --region us-east-1 --output text 2>/dev/null || echo "")
CATALOG_EVENTS_TOPIC_ARN=$(awslocal sns create-topic --name catalog-events-topic --region us-east-1 --output text 2>/dev/null || echo "")

# Checkout Flow SNS Topics
CHECKOUT_TOPIC_ARN=$(awslocal sns create-topic --name gearify-checkout-events --region us-east-1 --output text 2>/dev/null || echo "")
SHIPPING_TOPIC_ARN=$(awslocal sns create-topic --name gearify-shipping-events --region us-east-1 --output text 2>/dev/null || echo "")

echo "  - Created topic: gearify-order-events"
echo "  - Created topic: gearify-payment-events"
echo "  - Created topic: gearify-inventory-events"
echo "  - Created topic: gearify-media-upload-events"
echo "  - Created topic: gearify-image-processing-completed"
echo "  - Created topic: catalog-events-topic"
echo "  - Created topic: gearify-checkout-events"
echo "  - Created topic: gearify-shipping-events"

# Subscribe SQS queue to SNS topic for media processing
if [ ! -z "$MEDIA_TOPIC_ARN" ]; then
  MEDIA_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-image-processing-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$MEDIA_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $MEDIA_TOPIC_ARN --protocol sqs --notification-endpoint $MEDIA_QUEUE_ARN --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed gearify-image-processing-queue to gearify-media-upload-events"
  fi
fi

# Subscribe SQS queue to SNS topic for product thumbnail updates
if [ ! -z "$IMAGE_PROCESSING_COMPLETED_TOPIC_ARN" ]; then
  PRODUCT_THUMBNAIL_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-product-thumbnail-update-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$PRODUCT_THUMBNAIL_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $IMAGE_PROCESSING_COMPLETED_TOPIC_ARN --protocol sqs --notification-endpoint $PRODUCT_THUMBNAIL_QUEUE_ARN --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed gearify-product-thumbnail-update-queue to gearify-image-processing-completed"
  fi
fi

# Subscribe Search Service SQS queue to Catalog Events SNS topic
if [ ! -z "$CATALOG_EVENTS_TOPIC_ARN" ]; then
  SEARCH_CATALOG_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/search-catalog-events-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SEARCH_CATALOG_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $CATALOG_EVENTS_TOPIC_ARN --protocol sqs --notification-endpoint $SEARCH_CATALOG_QUEUE_ARN --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed search-catalog-events-queue to catalog-events-topic"
  fi
fi

# ==========================================
# Checkout Flow - SNS to SQS Subscriptions
# ==========================================
echo ""
echo "Creating checkout flow subscriptions..."

# Subscribe order-created-queue to order-events topic (payment-svc listens)
if [ ! -z "$ORDER_TOPIC_ARN" ]; then
  ORDER_CREATED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-created-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$ORDER_CREATED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $ORDER_TOPIC_ARN --protocol sqs --notification-endpoint $ORDER_CREATED_QUEUE_ARN --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed gearify-order-created-queue to gearify-order-events"
  fi
fi

# Subscribe order-payment-events-queue to payment-events topic (order-svc listens)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  ORDER_PAYMENT_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/order-payment-events-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$ORDER_PAYMENT_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $ORDER_PAYMENT_QUEUE_ARN --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed order-payment-events-queue to gearify-payment-events"
  fi
fi

# Subscribe notification-payment-events-queue to payment-events topic (notification-svc listens)
if [ ! -z "$PAYMENT_TOPIC_ARN" ]; then
  NOTIFICATION_PAYMENT_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/notification-payment-events-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$NOTIFICATION_PAYMENT_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $PAYMENT_TOPIC_ARN --protocol sqs --notification-endpoint $NOTIFICATION_PAYMENT_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"PaymentCompletedEvent\",\"PaymentFailedEvent\"]}"}' --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed notification-payment-events-queue to gearify-payment-events (PaymentCompleted + PaymentFailed filter)"
  fi
fi

# Subscribe shipping queues to shipping-events topic (order-svc listens)
if [ ! -z "$SHIPPING_TOPIC_ARN" ]; then
  SHIPPING_CREATED_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-created-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SHIPPING_CREATED_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $SHIPPING_TOPIC_ARN --protocol sqs --notification-endpoint $SHIPPING_CREATED_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"ShipmentCreated\"]}"}' --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed gearify-shipping-created-queue to gearify-shipping-events (ShipmentCreated filter)"
  fi

  SHIPPING_STATUS_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-shipping-status-queue --attribute-names QueueArn --region us-east-1 --output text --query 'Attributes.QueueArn' 2>/dev/null || echo "")
  if [ ! -z "$SHIPPING_STATUS_QUEUE_ARN" ]; then
    awslocal sns subscribe --topic-arn $SHIPPING_TOPIC_ARN --protocol sqs --notification-endpoint $SHIPPING_STATUS_QUEUE_ARN --attributes '{"FilterPolicy":"{\"EventType\":[\"ShipmentStatusUpdated\",\"ShipmentDelivered\"]}"}' --region us-east-1 2>/dev/null || echo "  - Failed to subscribe queue to topic"
    echo "  - Subscribed gearify-shipping-status-queue to gearify-shipping-events (status updates filter)"
  fi
fi

echo "Checkout flow subscriptions created successfully!"

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
# OpenSearch Domain
# ==========================================
echo ""
echo "Creating OpenSearch domain..."

awslocal opensearch create-domain \
  --domain-name gearify-search \
  --engine-version OpenSearch_2.5 \
  --cluster-config InstanceType=t3.small.search,InstanceCount=1 \
  --ebs-options EBSEnabled=true,VolumeType=gp2,VolumeSize=10 \
  --region us-east-1 \
  2>/dev/null || echo "  - OpenSearch domain gearify-search already exists"

echo "OpenSearch domain created successfully!"

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

# Seed sort options for default tenant
echo "  - Seeding sort options for default tenant"
awslocal dynamodb batch-write-item \
  --request-items file://${CONFIG_DIR}/dynamodb/data/sort-options-default-tenant.json \
  --region us-east-1 \
  2>/dev/null || echo "    Failed to seed sort options"
