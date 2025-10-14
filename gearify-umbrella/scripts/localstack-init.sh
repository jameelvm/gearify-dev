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
  --attribute-definitions AttributeName=tenantId,AttributeType=S AttributeName=productId,AttributeType=S \
  --key-schema AttributeName=tenantId,KeyType=HASH AttributeName=productId,KeyType=RANGE \
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
