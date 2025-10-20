#!/usr/bin/env bash
set -e

echo "🚀 Initializing LocalStack resources..."

# Wait for LocalStack
until awslocal --version > /dev/null 2>&1; do
    echo "Waiting for LocalStack..."
    sleep 2
done

# Create Cognito User Pool
echo "Creating Cognito User Pool..."
USER_POOL_ID=$(awslocal cognito-idp create-user-pool \
  --pool-name gearify-users \
  --username-attributes email \
  --auto-verified-attributes email \
  --query 'UserPool.Id' \
  --output text 2>/dev/null || echo "")

if [ -z "$USER_POOL_ID" ]; then
    USER_POOL_ID=$(awslocal cognito-idp list-user-pools --max-results 10 --query "UserPools[?Name=='gearify-users'].Id" --output text)
fi

echo "User Pool ID: $USER_POOL_ID"

# Create Cognito User Pool Client
CLIENT_ID=$(awslocal cognito-idp create-user-pool-client \
  --user-pool-id "$USER_POOL_ID" \
  --client-name gearify-web-client \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --query 'UserPoolClient.ClientId' \
  --output text 2>/dev/null || echo "")

if [ -z "$CLIENT_ID" ]; then
    CLIENT_ID=$(awslocal cognito-idp list-user-pool-clients --user-pool-id "$USER_POOL_ID" --query "UserPoolClients[0].ClientId" --output text)
fi

echo "Client ID: $CLIENT_ID"

# Create demo users
echo "Creating demo users..."
awslocal cognito-idp admin-create-user \
  --user-pool-id "$USER_POOL_ID" \
  --username admin@gearify.com \
  --user-attributes Name=email,Value=admin@gearify.com Name=email_verified,Value=true \
  --temporary-password "TempPass123!" \
  --message-action SUPPRESS 2>/dev/null || echo "User already exists"

awslocal cognito-idp admin-set-user-password \
  --user-pool-id "$USER_POOL_ID" \
  --username admin@gearify.com \
  --password "Admin123!" \
  --permanent 2>/dev/null || echo "Password already set"

awslocal cognito-idp admin-create-user \
  --user-pool-id "$USER_POOL_ID" \
  --username user@global-demo.com \
  --user-attributes Name=email,Value=user@global-demo.com Name=email_verified,Value=true \
  --temporary-password "TempPass123!" \
  --message-action SUPPRESS 2>/dev/null || echo "User already exists"

awslocal cognito-idp admin-set-user-password \
  --user-pool-id "$USER_POOL_ID" \
  --username user@global-demo.com \
  --password "User123!" \
  --permanent 2>/dev/null || echo "Password already set"

# Create DynamoDB Tables
echo "Creating DynamoDB tables..."
awslocal dynamodb create-table \
  --table-name gearify-products \
  --attribute-definitions \
    AttributeName=PK,AttributeType=S \
    AttributeName=SK,AttributeType=S \
    AttributeName=GSI1PK,AttributeType=S \
    AttributeName=GSI1SK,AttributeType=S \
  --key-schema AttributeName=PK,KeyType=HASH AttributeName=SK,KeyType=RANGE \
  --global-secondary-indexes \
    "[{\"IndexName\":\"GSI1\",\"KeySchema\":[{\"AttributeName\":\"GSI1PK\",\"KeyType\":\"HASH\"},{\"AttributeName\":\"GSI1SK\",\"KeyType\":\"RANGE\"}],\"Projection\":{\"ProjectionType\":\"ALL\"},\"ProvisionedThroughput\":{\"ReadCapacityUnits\":5,\"WriteCapacityUnits\":5}}]" \
  --billing-mode PAY_PER_REQUEST 2>/dev/null || echo "Table exists"

awslocal dynamodb create-table \
  --table-name gearify-orders \
  --attribute-definitions AttributeName=tenantId,AttributeType=S AttributeName=orderId,AttributeType=S \
  --key-schema AttributeName=tenantId,KeyType=HASH AttributeName=orderId,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST 2>/dev/null || echo "Table exists"

awslocal dynamodb create-table \
  --table-name gearify-tenants \
  --attribute-definitions AttributeName=tenantId,AttributeType=S \
  --key-schema AttributeName=tenantId,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST 2>/dev/null || echo "Table exists"

awslocal dynamodb create-table \
  --table-name gearify-feature-flags \
  --attribute-definitions AttributeName=tenantId,AttributeType=S AttributeName=flagKey,AttributeType=S \
  --key-schema AttributeName=tenantId,KeyType=HASH AttributeName=flagKey,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST 2>/dev/null || echo "Table exists"

# Create S3 Buckets
echo "Creating S3 buckets..."
awslocal s3 mb s3://gearify-product-images 2>/dev/null || echo "Bucket exists"
awslocal s3 mb s3://gearify-assets 2>/dev/null || echo "Bucket exists"

# Create SQS Queues
echo "Creating SQS queues..."
awslocal sqs create-queue --queue-name gearify-order-events 2>/dev/null || echo "Queue exists"
awslocal sqs create-queue --queue-name gearify-payment-events 2>/dev/null || echo "Queue exists"
awslocal sqs create-queue --queue-name gearify-notification-events 2>/dev/null || echo "Queue exists"

# Create SNS Topics
echo "Creating SNS topics..."
ORDER_TOPIC_ARN=$(awslocal sns create-topic --name gearify-order-topic --query 'TopicArn' --output text 2>/dev/null || awslocal sns list-topics --query "Topics[?contains(TopicArn, 'gearify-order-topic')].TopicArn" --output text)
PAYMENT_TOPIC_ARN=$(awslocal sns create-topic --name gearify-payment-topic --query 'TopicArn' --output text 2>/dev/null || awslocal sns list-topics --query "Topics[?contains(TopicArn, 'gearify-payment-topic')].TopicArn" --output text)

# Subscribe SQS to SNS
echo "Subscribing SQS queues to SNS topics..."
ORDER_QUEUE_ARN=$(awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/gearify-order-events --attribute-names QueueArn --query 'Attributes.QueueArn' --output text)

awslocal sns subscribe \
  --topic-arn "$ORDER_TOPIC_ARN" \
  --protocol sqs \
  --notification-endpoint "$ORDER_QUEUE_ARN" 2>/dev/null || echo "Subscription exists"

# Create Secrets
echo "Creating secrets..."
awslocal secretsmanager create-secret \
  --name gearify/stripe-api-key \
  --secret-string "sk_test_local_stripe_key" 2>/dev/null || echo "Secret exists"

awslocal secretsmanager create-secret \
  --name gearify/paypal-client-secret \
  --secret-string "paypal_local_secret" 2>/dev/null || echo "Secret exists"

# Create Parameter Store values
echo "Creating SSM parameters..."
awslocal ssm put-parameter \
  --name /gearify/default-tenant-id \
  --value "default" \
  --type String \
  --overwrite 2>/dev/null || echo "Parameter exists"

awslocal ssm put-parameter \
  --name /gearify/demo-tenant-id \
  --value "global-demo" \
  --type String \
  --overwrite 2>/dev/null || echo "Parameter exists"

# Seed DynamoDB with dummy data
echo "Seeding DynamoDB tables with dummy data..."

# Helper function to insert product
insert_product() {
  local tenant_id=$1
  local product_id=$2
  local sku=$3
  local name=$4
  local description=$5
  local category=$6
  local brand=$7
  local price=$8
  local compare_at_price=$9
  local is_active=${10}

  awslocal dynamodb put-item \
    --table-name gearify-products \
    --item "{
      \"PK\": {\"S\": \"TENANT#$tenant_id\"},
      \"SK\": {\"S\": \"PRODUCT#$product_id\"},
      \"GSI1PK\": {\"S\": \"TENANT#$tenant_id#CATEGORY#$category\"},
      \"GSI1SK\": {\"S\": \"PRODUCT#$product_id\"},
      \"Id\": {\"S\": \"$product_id\"},
      \"TenantId\": {\"S\": \"$tenant_id\"},
      \"Sku\": {\"S\": \"$sku\"},
      \"Name\": {\"S\": \"$name\"},
      \"Description\": {\"S\": \"$description\"},
      \"Category\": {\"S\": \"$category\"},
      \"Brand\": {\"S\": \"$brand\"},
      \"Price\": {\"N\": \"$price\"},
      \"CompareAtPrice\": {\"N\": \"$compare_at_price\"},
      \"Currency\": {\"S\": \"USD\"},
      \"IsActive\": {\"BOOL\": $is_active},
      \"Tags\": {\"SS\": [\"featured\", \"$category\"]},
      \"CreatedAt\": {\"S\": \"$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)\"},
      \"UpdatedAt\": {\"S\": \"$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)\"}
    }" 2>/dev/null || echo "Product $sku already exists"
}

# Seed products for default-tenant
echo "  → Seeding products for default-tenant..."
insert_product "default-tenant" "prod-001" "GR-CAM-001" "4K Action Camera" "Waterproof 4K action camera with image stabilization" "Electronics" "Gearify" "299.99" "349.99" "true"
insert_product "default-tenant" "prod-002" "GR-TENT-002" "3-Person Camping Tent" "Lightweight waterproof tent perfect for camping trips" "Camping" "Gearify" "149.99" "199.99" "true"
insert_product "default-tenant" "prod-003" "GR-BACK-003" "Hiking Backpack 40L" "Durable hiking backpack with multiple compartments" "Camping" "Gearify" "89.99" "119.99" "true"
insert_product "default-tenant" "prod-004" "GR-BOOT-004" "Trekking Boots" "All-terrain waterproof trekking boots" "Footwear" "Gearify" "179.99" "229.99" "true"
insert_product "default-tenant" "prod-005" "GR-DRONE-005" "GPS Drone with 4K Camera" "Professional GPS drone with 30min flight time" "Electronics" "Gearify" "599.99" "749.99" "true"
insert_product "default-tenant" "prod-006" "GR-SLEEP-006" "Sleeping Bag -10°C" "4-season sleeping bag rated for -10°C" "Camping" "Gearify" "129.99" "159.99" "true"
insert_product "default-tenant" "prod-007" "GR-BIKE-007" "Mountain Bike 29\"" "Full suspension mountain bike" "Bikes" "Gearify" "1299.99" "1599.99" "true"
insert_product "default-tenant" "prod-008" "GR-WATCH-008" "GPS Sports Watch" "Multi-sport GPS watch with heart rate monitor" "Electronics" "Gearify" "349.99" "449.99" "true"

# Seed products for test-tenant
echo "  → Seeding products for test-tenant..."
insert_product "test-tenant" "prod-101" "TST-LAP-001" "Gaming Laptop 17\"" "High-performance gaming laptop with RTX 4080" "Computers" "TestBrand" "2199.99" "2499.99" "true"
insert_product "test-tenant" "prod-102" "TST-DESK-002" "Standing Desk" "Electric height-adjustable standing desk" "Furniture" "TestBrand" "499.99" "649.99" "true"
insert_product "test-tenant" "prod-103" "TST-CHAIR-003" "Ergonomic Office Chair" "Mesh ergonomic office chair with lumbar support" "Furniture" "TestBrand" "349.99" "449.99" "true"
insert_product "test-tenant" "prod-104" "TST-PHONE-004" "Flagship Smartphone" "Latest flagship smartphone with 5G" "Electronics" "TestBrand" "999.99" "1199.99" "true"
insert_product "test-tenant" "prod-105" "TST-HEAD-005" "Wireless Headphones" "Noise-cancelling wireless headphones" "Audio" "TestBrand" "299.99" "349.99" "true"

# Seed products for acme-corp
echo "  → Seeding products for acme-corp..."
insert_product "acme-corp" "prod-201" "ACME-PROJ-001" "4K Projector" "Business 4K projector 5000 lumens" "Office" "Acme" "1899.99" "2199.99" "true"
insert_product "acme-corp" "prod-202" "ACME-CONF-002" "Conference Phone" "360° conference speakerphone" "Office" "Acme" "449.99" "549.99" "true"
insert_product "acme-corp" "prod-203" "ACME-PRINT-003" "Laser Printer" "Color laser printer with duplex" "Office" "Acme" "599.99" "749.99" "true"
insert_product "acme-corp" "prod-204" "ACME-SCAN-004" "Document Scanner" "High-speed document scanner" "Office" "Acme" "349.99" "449.99" "true"

# Seed tenant information
echo "Seeding tenant information..."
awslocal dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "tenantId": {"S": "default-tenant"},
    "name": {"S": "Default Tenant"},
    "status": {"S": "active"},
    "plan": {"S": "free"},
    "settings": {"M": {
      "currency": {"S": "USD"},
      "timezone": {"S": "UTC"}
    }},
    "createdAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"},
    "updatedAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"}
  }' 2>/dev/null || echo "Tenant default-tenant already exists"

awslocal dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "tenantId": {"S": "test-tenant"},
    "name": {"S": "Test Tenant Inc"},
    "status": {"S": "active"},
    "plan": {"S": "pro"},
    "settings": {"M": {
      "currency": {"S": "USD"},
      "timezone": {"S": "America/New_York"}
    }},
    "createdAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"},
    "updatedAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"}
  }' 2>/dev/null || echo "Tenant test-tenant already exists"

awslocal dynamodb put-item \
  --table-name gearify-tenants \
  --item '{
    "tenantId": {"S": "acme-corp"},
    "name": {"S": "Acme Corporation"},
    "status": {"S": "active"},
    "plan": {"S": "enterprise"},
    "settings": {"M": {
      "currency": {"S": "USD"},
      "timezone": {"S": "America/Los_Angeles"}
    }},
    "createdAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"},
    "updatedAt": {"S": "'$(date -u +%Y-%m-%dT%H:%M:%S.%3NZ)'"}
  }' 2>/dev/null || echo "Tenant acme-corp already exists"

echo "✅ Dummy data seeded successfully!"

# Save to .env.localstack
cat > /tmp/.env.localstack <<EOF
COGNITO_USER_POOL_ID=$USER_POOL_ID
COGNITO_CLIENT_ID=$CLIENT_ID
ORDER_TOPIC_ARN=$ORDER_TOPIC_ARN
PAYMENT_TOPIC_ARN=$PAYMENT_TOPIC_ARN
EOF

echo "✅ LocalStack initialization complete!"
echo "User Pool ID: $USER_POOL_ID"
echo "Client ID: $CLIENT_ID"
echo "Demo users: admin@gearify.com / Admin123!"
echo "            user@global-demo.com / User123!"
