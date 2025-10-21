# LocalStack Internal Server Error - Resolution Guide

## Issue Summary

You're experiencing an "internal server error" when querying products from the Catalog service using DynamoDB with LocalStack.

## What I've Verified

✅ **LocalStack is running** on http://localhost:4566
✅ **DynamoDB service is active** in LocalStack
✅ **Table "gearify-products" exists** with 17 items
✅ **Service builds successfully** with no errors
✅ **Query handlers are implemented** correctly
✅ **Repository implementation is correct**

## Root Cause (Most Likely)

The service is **NOT loading the LocalStack configuration** because:

**ASPNETCORE_ENVIRONMENT is not set to "Development"**

This causes:
1. `appsettings.Development.json` is ignored
2. LocalStack configuration is never loaded
3. AWS SDK uses default credential chain (IAM roles)
4. Error: "Unable to get IAM security credentials from EC2 Instance Metadata Service"

## Solution

### Option 1: Use the PowerShell Helper Script (EASIEST)

I've created a script that handles everything for you:

```powershell
.\run-catalog-dev.ps1
```

This script will:
- Set ASPNETCORE_ENVIRONMENT=Development
- Verify LocalStack is running
- Verify the DynamoDB table exists
- Run the service with proper configuration
- Show diagnostic output

### Option 2: Manual PowerShell

```powershell
# IMPORTANT: Run these in the SAME PowerShell window

# 1. Stop any currently running catalog service
# Press Ctrl+C or kill the process

# 2. Set environment variable
$env:ASPNETCORE_ENVIRONMENT="Development"

# 3. Verify it's set
echo $env:ASPNETCORE_ENVIRONMENT
# Should output: Development

# 4. Run the service
dotnet run --project gearify-catalog-svc
```

### Option 3: Bash/Linux

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project gearify-catalog-svc
```

## What to Look For

When you run the service, you should see these **diagnostic messages** in the console:

```
=== LocalStack Configuration ===
UseLocalStack: True
LocalStackHost: localhost:4566
AWS Region: us-east-1
Environment: Development
================================
AWS Options - Region: USEast1
AWS Options - ServiceURL: http://localhost:4566
AWS services registered successfully
```

### Additional DynamoDB Diagnostics

When you call the products endpoint, you'll now see:

```
[DynamoDbProductRepository] Querying products for tenant: tenant1
[DynamoDbProductRepository] Table name: gearify-products
[DynamoDbProductRepository] DynamoDB client endpoint: http://localhost:4566
[DynamoDbProductRepository] Executing query...
[DynamoDbProductRepository] Query successful. Items returned: X
```

## Testing the Service

Once the service is running with correct configuration:

### 1. Health Check
```bash
curl http://localhost:5000/health
```

Expected response:
```json
{"status":"healthy","service":"catalog"}
```

### 2. Get All Products
```bash
curl http://localhost:5000/api/catalog/products -H "X-Tenant-Id: tenant1"
```

Expected: JSON array of products

### 3. Using Swagger UI
Navigate to: http://localhost:5000/swagger

Click "Authorize" and add the X-Tenant-Id header.

## What I Changed

### 1. Added Diagnostic Logging to Startup.cs

The service now logs all LocalStack configuration values on startup:
- UseLocalStack setting
- LocalStackHost
- AWS Region
- Environment name
- AWS Options details

**Location:** `gearify-catalog-svc/Startup.cs:35-45`

### 2. Added DynamoDB Query Diagnostics

The repository now logs detailed information during DynamoDB queries:
- Tenant ID being queried
- Table name
- DynamoDB endpoint URL
- Query execution status
- Error details if query fails

**Location:** `gearify-catalog-svc/Infrastructure/Repositories/DynamoDbProductRepository.cs:34-67`

### 3. Created Helper Scripts

- **run-catalog-dev.ps1** - Automated script to run service with LocalStack
- **DIAGNOSE_CATALOG_LOCALSTACK.md** - Detailed troubleshooting guide

## If You See Different Output

### Scenario 1: UseLocalStack is False

```
=== LocalStack Configuration ===
UseLocalStack: False  <-- PROBLEM!
LocalStackHost:
```

**Fix:** Environment is not set to Development. Follow Solution steps above.

### Scenario 2: DynamoDB Endpoint is Null

```
[DynamoDbProductRepository] DynamoDB client endpoint:
```

**Fix:** LocalStack configuration is not being applied. Verify:
1. LocalStack.Client.Extensions package is installed
2. appsettings.Development.json has correct configuration
3. ASPNETCORE_ENVIRONMENT=Development is set

### Scenario 3: Connection Refused Error

```
[DynamoDbProductRepository] ERROR: HttpRequestException
Message: Connection refused
```

**Fix:** LocalStack is not running. Start it:
```bash
docker run --rm -p 4566:4566 localstack/localstack
```

## Common Mistakes to Avoid

### ❌ Setting environment in different terminal
Don't set the environment variable in one PowerShell window and run the service in another.

### ❌ Running service before setting environment
Don't run `dotnet run` first, then try to set the environment.

### ❌ Using Production environment
Don't use `ASPNETCORE_ENVIRONMENT=Production` (LocalStack config won't load).

### ✅ Correct Approach
Set environment → Verify it's set → Run service (all in same session).

## Files Reference

### Configuration Files
- `gearify-catalog-svc/appsettings.Development.json` - LocalStack configuration
- `gearify-catalog-svc/Startup.cs` - Service registration with diagnostics
- `gearify-catalog-svc/Infrastructure/Repositories/DynamoDbProductRepository.cs` - DynamoDB repository with logging

### Helper Files (Created)
- `run-catalog-dev.ps1` - Automated startup script
- `DIAGNOSE_CATALOG_LOCALSTACK.md` - Detailed diagnostic guide
- `LOCALSTACK_TROUBLESHOOTING.md` - General LocalStack troubleshooting
- `LOCALSTACK_ISSUE_RESOLUTION.md` - This file

## Next Steps

1. **Stop** the currently running catalog service
2. **Run** the service using `run-catalog-dev.ps1` OR manually set environment
3. **Check** console output for diagnostic messages
4. **Test** the endpoints with curl or Swagger
5. **Report** any errors you see with the full console output

## Expected Success Output

When everything is working, you should see:

```
=== LocalStack Configuration ===
UseLocalStack: True
LocalStackHost: localhost:4566
AWS Region: us-east-1
Environment: Development
================================
AWS Options - Region: USEast1
AWS Options - ServiceURL: http://localhost:4566
AWS services registered successfully

info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

Then when you call `/api/catalog/products`:

```
[DynamoDbProductRepository] Querying products for tenant: tenant1
[DynamoDbProductRepository] Table name: gearify-products
[DynamoDbProductRepository] DynamoDB client endpoint: http://localhost:4566
[DynamoDbProductRepository] Executing query...
[DynamoDbProductRepository] Query successful. Items returned: 17
```

## Still Getting Errors?

If you're still experiencing issues after following these steps, please provide:

1. **Full console output** from service startup
2. **Diagnostic messages** shown above
3. **Exact error message** from the API call
4. **Confirmation** that you ran: `$env:ASPNETCORE_ENVIRONMENT="Development"`
5. **LocalStack health check** output: `curl http://localhost:4566/_localstack/health`

The diagnostic logging I added will help us identify the exact issue.
