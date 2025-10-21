# AWS Profile Setup for LocalStack - COMPLETE ✓

## What Was Done

I've successfully configured an AWS profile named "localstack" for use with LocalStack across all your services.

## Files Created

### 1. AWS Credentials File
**Location:** `~/.aws/credentials` (Linux/Mac) or `C:\Users\{username}\.aws\credentials` (Windows)

```ini
[localstack]
aws_access_key_id = test
aws_secret_access_key = test
```

### 2. AWS Config File
**Location:** `~/.aws/config`

```ini
[profile localstack]
region = us-east-1
output = json
```

## Services Updated

All 10 services now have the AWS profile configured in their `appsettings.Development.json`:

✓ **gearify-catalog-svc** - Uses DynamoDB and S3
✓ **gearify-cart-svc** - Uses DynamoDB
✓ **gearify-tenant-svc** - Ready for AWS services
✓ **gearify-order-svc** - Ready for AWS services
✓ **gearify-payment-svc** - Ready for AWS services
✓ **gearify-shipping-svc** - Ready for AWS services
✓ **gearify-inventory-svc** - Ready for AWS services
✓ **gearify-media-svc** - Ready for AWS services (S3)
✓ **gearify-notification-svc** - Ready for AWS services (SES, SNS)
✓ **gearify-search-svc** - Ready for AWS services

## Configuration Format

Each service's `appsettings.Development.json` now contains:

```json
{
  "LocalStack": {
    "UseLocalStack": true,
    "Session": {
      "AwsAccessKeyId": "test",
      "AwsAccessKey": "test",
      "AwsSecretAccessKey": "test"
    },
    "Config": {
      "LocalStackHost": "localhost:4566"
    }
  },
  "AWS": {
    "Profile": "localstack",
    "Region": "us-east-1",
    "ServiceURL": "http://localhost:4566"
  }
}
```

## How It Works

### LocalStack.Client.Extensions Integration

When you run a service with `ASPNETCORE_ENVIRONMENT=Development`:

1. **appsettings.Development.json is loaded**
2. **LocalStack.Client.Extensions checks** `LocalStack:UseLocalStack` setting
3. **If true**, it:
   - Reads credentials from `LocalStack:Session` section
   - Uses profile from `AWS:Profile` setting
   - Overrides ServiceURL to point to LocalStack (`http://localhost:4566`)
   - Configures all AWS services to use LocalStack instead of real AWS

4. **AWS SDK clients** (DynamoDB, S3, etc.) are automatically configured to:
   - Use test credentials (`test` / `test`)
   - Point to `http://localhost:4566`
   - Use region `us-east-1`

### Development vs Production

**Development (LocalStack):**
```json
"AWS": {
  "Profile": "localstack",
  "Region": "us-east-1",
  "ServiceURL": "http://localhost:4566"
}
```
- Uses `~/.aws/credentials` with `[localstack]` profile
- Points to LocalStack on localhost
- Uses test credentials

**Production (Real AWS):**
```json
"AWS": {
  "Region": "us-east-1"
}
```
- No Profile needed (uses IAM roles)
- No ServiceURL (uses real AWS endpoints)
- No credentials in config (uses EC2/ECS instance roles)

## Benefits of Using AWS Profile

### ✅ Security
- Credentials stored in standard AWS location (`~/.aws/credentials`)
- Not hardcoded in application code
- Separate from production credentials

### ✅ Consistency
- Same pattern as AWS CLI and other AWS tools
- Standard AWS SDK credential resolution
- Works with `aws` CLI commands

### ✅ Flexibility
- Easy to switch between profiles
- Can have multiple LocalStack profiles (different regions, etc.)
- Compatible with AWS best practices

### ✅ Team Development
- Each developer can have their own `~/.aws/credentials`
- Config files (appsettings.Development.json) can be committed to git
- No credential leaks in source control

## Testing the Setup

### 1. Verify Profile Exists
```bash
# Linux/Mac
cat ~/.aws/credentials

# Windows
type %USERPROFILE%\.aws\credentials
```

Should show:
```
[localstack]
aws_access_key_id = test
aws_secret_access_key = test
```

### 2. Test with AWS CLI (Optional)
```bash
# Set profile
export AWS_PROFILE=localstack

# Test against LocalStack
aws --endpoint-url=http://localhost:4566 dynamodb list-tables

# Should return: {"TableNames": [...]}
```

### 3. Run Catalog Service
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Development  # or $env:ASPNETCORE_ENVIRONMENT="Development" in PowerShell

# Run service
dotnet run --project gearify-catalog-svc
```

Should see in console:
```
=== LocalStack Configuration ===
UseLocalStack: True
LocalStackHost: localhost:4566
AWS Region: us-east-1
Environment: Development
================================
AWS Options - ServiceURL: http://localhost:4566
AWS services registered successfully
```

### 4. Test API Endpoint
```bash
curl http://localhost:5000/api/catalog/products -H "X-Tenant-Id: tenant1"
```

Should return product data from LocalStack DynamoDB.

## Troubleshooting

### Issue: "Profile 'localstack' not found"

**Solution:** Verify files exist and are formatted correctly:
```bash
cat ~/.aws/credentials
cat ~/.aws/config
```

### Issue: Still getting "Unable to get IAM credentials"

**Solution:** Check that:
1. `ASPNETCORE_ENVIRONMENT=Development` is set
2. LocalStack is running (`curl http://localhost:4566/_localstack/health`)
3. Service is using the rebuilt version

### Issue: "Access Denied" errors from LocalStack

**Solution:** LocalStack doesn't enforce real AWS permissions. If you see this:
- Check that ServiceURL is pointing to `http://localhost:4566`
- Verify LocalStack is running and healthy
- Check LocalStack logs for actual errors

## Using the Helper Scripts

I've created scripts that handle environment setup automatically:

### Run Catalog Service with Diagnostics
```bash
./run-catalog-dev.ps1
```

This script:
- Sets `ASPNETCORE_ENVIRONMENT=Development`
- Verifies LocalStack is running
- Verifies DynamoDB table exists
- Runs the service
- Shows all diagnostic output

### Check LocalStack Status
```bash
./check-localstack-status.ps1
```

This script verifies:
- LocalStack is running
- DynamoDB service is active
- Required tables exist

## Production Deployment

For production, ensure `appsettings.Production.json` or `appsettings.json` has:

```json
{
  "AWS": {
    "Region": "us-east-1"
  }
}
```

**Do NOT include:**
- `Profile` setting (use IAM roles instead)
- `ServiceURL` (let AWS SDK use default endpoints)
- `LocalStack` section

The service will automatically use:
- **EC2 Instance Profile** (if running on EC2)
- **ECS Task Role** (if running on ECS/Fargate)
- **EKS IRSA** (if running on Kubernetes)
- **Environment variables** (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)

## Summary

✅ AWS profile "localstack" created in `~/.aws/credentials` and `~/.aws/config`
✅ All 10 services updated to use the profile in Development mode
✅ LocalStack.Client.Extensions configured correctly
✅ Catalog service built successfully with diagnostics
✅ Ready to test with `run-catalog-dev.ps1`

The profile-based approach is the correct, industry-standard way to manage AWS credentials for local development!
