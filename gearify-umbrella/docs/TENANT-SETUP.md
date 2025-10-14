# Adding a New Tenant

## Overview
This guide walks through setting up a new tenant from start to finish.

## Step 1: Add Tenant Configuration

### Update tenants.json
Edit `scripts/seed/tenants.json`:

```json
{
  "tenantId": "my-new-tenant",
  "name": "My Cricket Shop",
  "domain": "myshop.gearify.com",
  "theme": {
    "primaryColor": "#e91e63",
    "secondaryColor": "#9c27b0",
    "logoUrl": "https://cdn.myshop.com/logo.png",
    "fontFamily": "Montserrat, sans-serif"
  },
  "features": {
    "enableCheckout": true,
    "enablePaypal": true,
    "enableStripe": true,
    "enableGuestCheckout": false
  }
}
```

## Step 2: Seed Tenant Data

### Run Seed Script
```bash
# Re-seed all data
make seed-clean

# Or manually insert into DynamoDB
node scripts/seed/seed-dynamodb.js
```

### Verify in DynamoDB
```bash
awslocal dynamodb get-item \
  --table-name gearify-tenants \
  --key '{"tenantId": {"S": "my-new-tenant"}}'
```

## Step 3: Configure DNS (Production)

### Route 53 Configuration
```bash
# Create DNS record
aws route53 change-resource-record-sets \
  --hosted-zone-id Z1234567890ABC \
  --change-batch file://dns-change.json
```

**dns-change.json:**
```json
{
  "Changes": [
    {
      "Action": "CREATE",
      "ResourceRecordSet": {
        "Name": "myshop.gearify.com",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "Z215JYRZR1TBD5",
          "DNSName": "your-alb.us-east-1.elb.amazonaws.com",
          "EvaluateTargetHealth": false
        }
      }
    }
  ]
}
```

### SSL Certificate
```bash
# Request certificate via ACM
aws acm request-certificate \
  --domain-name myshop.gearify.com \
  --validation-method DNS
```

## Step 4: Set Feature Flags

### Enable/Disable Features
```bash
# Enable Stripe for tenant
awslocal dynamodb put-item \
  --table-name gearify-feature-flags \
  --item '{
    "tenantId": {"S": "my-new-tenant"},
    "flagKey": {"S": "enable-stripe"},
    "enabled": {"BOOL": true},
    "updatedAt": {"S": "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'"}
  }'

# Disable PayPal
awslocal dynamodb put-item \
  --table-name gearify-feature-flags \
  --item '{
    "tenantId": {"S": "my-new-tenant"},
    "flagKey": {"S": "enable-paypal"},
    "enabled": {"BOOL": false},
    "updatedAt": {"S": "'$(date -u +"%Y-%m-%dT%H:%M:%SZ")'"}
  }'
```

## Step 5: Add Product Catalog

### Bulk Import Products
```javascript
// add-products.js
const { DynamoDBClient } = require('@aws-sdk/client-dynamodb');
const { DynamoDBDocumentClient, BatchWriteCommand } = require('@aws-sdk/lib-dynamodb');

const client = DynamoDBDocumentClient.from(new DynamoDBClient({
  region: 'us-east-1',
  endpoint: 'http://localhost:4566',
  credentials: { accessKeyId: 'test', secretAccessKey: 'test' }
}));

const products = [
  {
    tenantId: 'my-new-tenant',
    productId: 'prod-my-new-tenant-1',
    name: 'Premium English Willow Bat',
    category: 'bats',
    price: 349.99,
    weight: 1190,
    grade: 'Grade 1+',
    weightType: 'Medium',
    stock: 25
  },
  // ... more products
];

const batchWrite = async () => {
  const requests = products.map(product => ({
    PutRequest: { Item: product }
  }));

  await client.send(new BatchWriteCommand({
    RequestItems: {
      'gearify-products': requests
    }
  }));
};

batchWrite().then(() => console.log('Products imported'));
```

## Step 6: Configure Payment Providers

### Stripe
1. Create connected account or use platform account
2. Store API keys in Secrets Manager:
```bash
aws secretsmanager create-secret \
  --name gearify/my-new-tenant/stripe-api-key \
  --secret-string "sk_live_xxxxxx"
```

### PayPal
1. Create app in PayPal Developer Dashboard
2. Store credentials:
```bash
aws secretsmanager create-secret \
  --name gearify/my-new-tenant/paypal-client-secret \
  --secret-string "xxxxx"
```

## Step 7: Set Up Shipping

### Configure Shipping Zones
```javascript
// Add to DynamoDB or separate shipping service
{
  tenantId: 'my-new-tenant',
  shippingZones: [
    {
      name: 'Domestic',
      countries: ['US'],
      rates: [
        { method: 'Standard', cost: 5.99, estimatedDays: 5 },
        { method: 'Express', cost: 15.99, estimatedDays: 2 }
      ]
    },
    {
      name: 'International',
      countries: ['GB', 'AU', 'CA'],
      rates: [
        { method: 'International', cost: 25.99, estimatedDays: 10 }
      ]
    }
  ]
}
```

## Step 8: Test Tenant Isolation

### Test API Calls
```bash
# Get tenant-specific products
curl http://localhost:8080/api/catalog/products \
  -H "Authorization: Bearer <jwt-token-with-tenantId>" \
  -H "Content-Type: application/json"

# Verify tenant theme
curl http://localhost:8080/api/tenants/my-new-tenant
```

### Test Frontend
1. Update hosts file (local):
```
127.0.0.1 myshop.localhost
```

2. Access: http://myshop.localhost:4200

3. Verify theme loads correctly

## Step 9: Create Admin User

### Cognito User
```bash
awslocal cognito-idp admin-create-user \
  --user-pool-id <pool-id> \
  --username admin@myshop.com \
  --user-attributes \
    Name=email,Value=admin@myshop.com \
    Name=email_verified,Value=true \
    Name=custom:tenantId,Value=my-new-tenant \
  --temporary-password "TempPass123!"

# Set permanent password
awslocal cognito-idp admin-set-user-password \
  --user-pool-id <pool-id> \
  --username admin@myshop.com \
  --password "AdminPass123!" \
  --permanent
```

## Step 10: Monitor & Validate

### Check Logs
```bash
# Filter logs by tenant
docker logs gearify-catalog-svc | grep "my-new-tenant"

# Or use Seq
# http://localhost:5341
# Filter: @Properties.TenantId = 'my-new-tenant'
```

### Verify Data Isolation
Query products from different tenants to ensure no cross-contamination:
```bash
# Should return only my-new-tenant products
awslocal dynamodb query \
  --table-name gearify-products \
  --key-condition-expression "tenantId = :tid" \
  --expression-attribute-values '{":tid": {"S": "my-new-tenant"}}'
```

## Checklist

- [ ] Tenant added to tenants.json
- [ ] Data seeded (tenant, products, feature flags)
- [ ] DNS configured (production only)
- [ ] SSL certificate issued
- [ ] Payment providers configured
- [ ] Shipping zones set up
- [ ] Feature flags configured
- [ ] Admin user created
- [ ] Tenant isolation tested
- [ ] Frontend theme verified
- [ ] Monitoring configured

## Rollback

If needed, remove tenant:
```bash
# Delete tenant
awslocal dynamodb delete-item \
  --table-name gearify-tenants \
  --key '{"tenantId": {"S": "my-new-tenant"}}'

# Delete products
# (Use scan + delete or drop/recreate table in non-prod)

# Revoke DNS and SSL certificate
```

## Troubleshooting

**Theme not loading:**
- Check browser cache
- Verify tenant-svc returns correct theme
- Inspect network tab for API call

**Products not showing:**
- Verify tenantId in JWT token
- Check DynamoDB for products with correct tenantId
- Review catalog-svc logs

**Payment issues:**
- Confirm API keys in Secrets Manager
- Check payment-svc logs for errors
- Verify Stripe/PayPal webhooks configured
