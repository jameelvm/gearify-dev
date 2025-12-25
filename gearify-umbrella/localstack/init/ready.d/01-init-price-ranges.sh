#!/bin/bash

# LocalStack initialization script for price ranges table
# This script runs automatically when LocalStack starts
# Place in: /etc/localstack/init/ready.d/ (or mount via docker-compose)

set -e

echo "========================================="
echo "  INITIALIZING PRICE RANGES TABLE"
echo "========================================="
echo ""

# LocalStack configuration
ENDPOINT="http://localhost:4566"
REGION="us-east-1"
TABLE_NAME="gearify-price-ranges"

# Counter for successful insertions
SUCCESS_COUNT=0
TOTAL_COUNT=5

echo "[$(date '+%Y-%m-%d %H:%M:%S')] Starting price ranges table initialization..."
echo ""

# Check if table already exists
echo "[INFO] Checking if table '$TABLE_NAME' exists..."
if awslocal dynamodb describe-table --table-name $TABLE_NAME 2>/dev/null; then
    echo "[INFO] ✓ Table '$TABLE_NAME' already exists - skipping creation"
    ITEM_COUNT=$(awslocal dynamodb scan --table-name $TABLE_NAME --select COUNT --output json | grep -o '"Count": [0-9]*' | grep -o '[0-9]*')
    echo "[INFO] Current item count: $ITEM_COUNT"

    if [ "$ITEM_COUNT" -ge "$TOTAL_COUNT" ]; then
        echo "[INFO] ✓ Table already has $ITEM_COUNT items - skipping seed"
        echo "[INFO] Initialization complete (table already seeded)"
        exit 0
    else
        echo "[INFO] Table has only $ITEM_COUNT items - proceeding with seed..."
    fi
else
    echo "[INFO] Table does not exist - creating..."

    # Create table
    awslocal dynamodb create-table \
        --table-name $TABLE_NAME \
        --attribute-definitions \
            AttributeName=PK,AttributeType=S \
            AttributeName=SK,AttributeType=S \
        --key-schema \
            AttributeName=PK,KeyType=HASH \
            AttributeName=SK,KeyType=RANGE \
        --billing-mode PAY_PER_REQUEST \
        --region $REGION

    if [ $? -eq 0 ]; then
        echo "[INFO] ✓ Table '$TABLE_NAME' created successfully"
    else
        echo "[ERROR] ✗ Failed to create table"
        exit 1
    fi
fi

echo ""
echo "[INFO] Seeding price ranges for tenant 'default'..."
echo "----------------------------------------"

# Seed price range 1: Under $50
echo -n "[1/$TOTAL_COUNT] Inserting: Under \$50... "
awslocal dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{
        "PK": {"S": "TENANT#default"},
        "SK": {"S": "PRICERANGE#a1b2c3d4-e5f6-7890-abcd-ef1234567890"},
        "Id": {"S": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"},
        "TenantId": {"S": "default"},
        "Label": {"S": "Under $50"},
        "MinPrice": {"N": "0"},
        "MaxPrice": {"N": "50"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "1"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "2024-01-01T00:00:00Z"},
        "UpdatedAt": {"S": "2024-01-01T00:00:00Z"},
        "CreatedBy": {"S": "system"},
        "UpdatedBy": {"S": "system"}
    }' 2>/dev/null && echo "✓" && SUCCESS_COUNT=$((SUCCESS_COUNT + 1)) || echo "✗"

# Seed price range 2: $50-$100
echo -n "[2/$TOTAL_COUNT] Inserting: \$50-\$100... "
awslocal dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{
        "PK": {"S": "TENANT#default"},
        "SK": {"S": "PRICERANGE#b2c3d4e5-f6a7-8901-bcde-f12345678901"},
        "Id": {"S": "b2c3d4e5-f6a7-8901-bcde-f12345678901"},
        "TenantId": {"S": "default"},
        "Label": {"S": "$50-$100"},
        "MinPrice": {"N": "50"},
        "MaxPrice": {"N": "100"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "2"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "2024-01-01T00:00:00Z"},
        "UpdatedAt": {"S": "2024-01-01T00:00:00Z"},
        "CreatedBy": {"S": "system"},
        "UpdatedBy": {"S": "system"}
    }' 2>/dev/null && echo "✓" && SUCCESS_COUNT=$((SUCCESS_COUNT + 1)) || echo "✗"

# Seed price range 3: $100-$250
echo -n "[3/$TOTAL_COUNT] Inserting: \$100-\$250... "
awslocal dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{
        "PK": {"S": "TENANT#default"},
        "SK": {"S": "PRICERANGE#c3d4e5f6-a7b8-9012-cdef-123456789012"},
        "Id": {"S": "c3d4e5f6-a7b8-9012-cdef-123456789012"},
        "TenantId": {"S": "default"},
        "Label": {"S": "$100-$250"},
        "MinPrice": {"N": "100"},
        "MaxPrice": {"N": "250"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "3"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "2024-01-01T00:00:00Z"},
        "UpdatedAt": {"S": "2024-01-01T00:00:00Z"},
        "CreatedBy": {"S": "system"},
        "UpdatedBy": {"S": "system"}
    }' 2>/dev/null && echo "✓" && SUCCESS_COUNT=$((SUCCESS_COUNT + 1)) || echo "✗"

# Seed price range 4: $250-$500
echo -n "[4/$TOTAL_COUNT] Inserting: \$250-\$500... "
awslocal dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{
        "PK": {"S": "TENANT#default"},
        "SK": {"S": "PRICERANGE#d4e5f6a7-b8c9-0123-def1-234567890123"},
        "Id": {"S": "d4e5f6a7-b8c9-0123-def1-234567890123"},
        "TenantId": {"S": "default"},
        "Label": {"S": "$250-$500"},
        "MinPrice": {"N": "250"},
        "MaxPrice": {"N": "500"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "4"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "2024-01-01T00:00:00Z"},
        "UpdatedAt": {"S": "2024-01-01T00:00:00Z"},
        "CreatedBy": {"S": "system"},
        "UpdatedBy": {"S": "system"}
    }' 2>/dev/null && echo "✓" && SUCCESS_COUNT=$((SUCCESS_COUNT + 1)) || echo "✗"

# Seed price range 5: $500 & Above
echo -n "[5/$TOTAL_COUNT] Inserting: \$500 & Above... "
awslocal dynamodb put-item \
    --table-name $TABLE_NAME \
    --item '{
        "PK": {"S": "TENANT#default"},
        "SK": {"S": "PRICERANGE#e5f6a7b8-c9d0-1234-ef12-345678901234"},
        "Id": {"S": "e5f6a7b8-c9d0-1234-ef12-345678901234"},
        "TenantId": {"S": "default"},
        "Label": {"S": "$500 & Above"},
        "MinPrice": {"N": "500"},
        "Currency": {"S": "USD"},
        "DisplayOrder": {"N": "5"},
        "IsActive": {"BOOL": true},
        "CreatedAt": {"S": "2024-01-01T00:00:00Z"},
        "UpdatedAt": {"S": "2024-01-01T00:00:00Z"},
        "CreatedBy": {"S": "system"},
        "UpdatedBy": {"S": "system"}
    }' 2>/dev/null && echo "✓" && SUCCESS_COUNT=$((SUCCESS_COUNT + 1)) || echo "✗"

echo ""
echo "========================================="
echo "  INITIALIZATION SUMMARY"
echo "========================================="
echo "  Table: $TABLE_NAME"
echo "  Tenant: default"
echo "  Records inserted: $SUCCESS_COUNT/$TOTAL_COUNT"
echo ""

if [ $SUCCESS_COUNT -eq $TOTAL_COUNT ]; then
    echo "[SUCCESS] ✓ All $SUCCESS_COUNT price ranges initialized!"
    exit 0
else
    echo "[WARNING] ⚠ Only $SUCCESS_COUNT/$TOTAL_COUNT records inserted"
    exit 1
fi
