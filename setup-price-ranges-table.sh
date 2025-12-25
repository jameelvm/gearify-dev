#!/bin/bash

# Setup script for gearify-price-ranges DynamoDB table
# This script creates the table and seeds it with initial data

echo "========================================="
echo "  GEARIFY PRICE RANGES TABLE SETUP"
echo "========================================="
echo ""

# LocalStack configuration
ENDPOINT="http://localhost:4566"
REGION="us-east-1"
TABLE_NAME="gearify-price-ranges"

# Set AWS credentials for LocalStack
export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test

# Counter for successful insertions
SUCCESS_COUNT=0
TOTAL_COUNT=5

echo "Configuration:"
echo "  • Endpoint: $ENDPOINT"
echo "  • Region: $REGION"
echo "  • Table: $TABLE_NAME"
echo ""

echo "Step 1: Creating DynamoDB table..."
echo "----------------------------------------"

aws dynamodb create-table \
    --table-name $TABLE_NAME \
    --attribute-definitions \
        AttributeName=PK,AttributeType=S \
        AttributeName=SK,AttributeType=S \
    --key-schema \
        AttributeName=PK,KeyType=HASH \
        AttributeName=SK,KeyType=RANGE \
    --billing-mode PAY_PER_REQUEST \
    --endpoint-url $ENDPOINT \
    --region $REGION 2>&1

if [ $? -eq 0 ]; then
    echo "✓ Table '$TABLE_NAME' created successfully"
else
    echo "⚠ Table may already exist - continuing with seeding..."
fi

echo ""
echo "Step 2: Seeding default tenant price ranges..."
echo "----------------------------------------"

# Seed price range 1: Under $50
aws dynamodb put-item \
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
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION

if [ $? -eq 0 ]; then
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    echo "✓ [1/$TOTAL_COUNT] Added: Under $50"
else
    echo "✗ [1/$TOTAL_COUNT] Failed to add: Under $50"
fi

# Seed price range 2: $50-$100
aws dynamodb put-item \
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
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION

if [ $? -eq 0 ]; then
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    echo "✓ [2/$TOTAL_COUNT] Added: \$50-\$100"
else
    echo "✗ [2/$TOTAL_COUNT] Failed to add: \$50-\$100"
fi

# Seed price range 3: $100-$250
aws dynamodb put-item \
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
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION

if [ $? -eq 0 ]; then
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    echo "✓ [3/$TOTAL_COUNT] Added: \$100-\$250"
else
    echo "✗ [3/$TOTAL_COUNT] Failed to add: \$100-\$250"
fi

# Seed price range 4: $250-$500
aws dynamodb put-item \
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
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION

if [ $? -eq 0 ]; then
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    echo "✓ [4/$TOTAL_COUNT] Added: \$250-\$500"
else
    echo "✗ [4/$TOTAL_COUNT] Failed to add: \$250-\$500"
fi

# Seed price range 5: $500 & Above
aws dynamodb put-item \
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
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION

if [ $? -eq 0 ]; then
    SUCCESS_COUNT=$((SUCCESS_COUNT + 1))
    echo "✓ [5/$TOTAL_COUNT] Added: \$500 & Above"
else
    echo "✗ [5/$TOTAL_COUNT] Failed to add: \$500 & Above"
fi

echo ""
echo "========================================="
echo "  INSERTION SUMMARY"
echo "========================================="
echo "  Successfully inserted: $SUCCESS_COUNT/$TOTAL_COUNT records"
if [ $SUCCESS_COUNT -eq $TOTAL_COUNT ]; then
    echo "  Status: ✓ ALL RECORDS INSERTED"
else
    echo "  Status: ⚠ SOME RECORDS FAILED"
fi
echo ""

echo "Step 3: Verifying data..."
echo "----------------------------------------"

aws dynamodb query \
    --table-name $TABLE_NAME \
    --key-condition-expression "PK = :pk AND begins_with(SK, :sk)" \
    --expression-attribute-values '{
        ":pk": {"S": "TENANT#default"},
        ":sk": {"S": "PRICERANGE#"}
    }' \
    --endpoint-url $ENDPOINT \
    --region $REGION \
    --output table

echo ""
echo "========================================="
echo "  SETUP COMPLETE!"
echo "========================================="
echo "  Table: $TABLE_NAME"
echo "  Tenant: default"
echo "  Records inserted: $SUCCESS_COUNT/$TOTAL_COUNT"
echo ""
if [ $SUCCESS_COUNT -eq $TOTAL_COUNT ]; then
    echo "✓ All price ranges seeded successfully!"
else
    echo "⚠ Warning: Some price ranges failed to insert"
    echo "  Check the logs above for details"
fi
echo ""
echo "To add price ranges for other tenants:"
echo "  Modify the PK value in the script"
echo "  Example: TENANT#acme, TENANT#contoso, etc."
echo "========================================="
