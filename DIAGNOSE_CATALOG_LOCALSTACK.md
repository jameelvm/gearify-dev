# Diagnosing Catalog Service LocalStack Connection

## Current Status

✅ **LocalStack is running** - Verified on http://localhost:4566
✅ **DynamoDB is available** - Service status: running
✅ **Table exists** - "gearify-products" table with 17 items
✅ **Service builds successfully** - No compilation errors
❌ **Getting internal server error** - When querying products

## Most Likely Issue

The service is running but **ASPNETCORE_ENVIRONMENT is not set to "Development"**, causing:

1. `appsettings.Development.json` is NOT being loaded
2. LocalStack configuration is ignored
3. AWS SDK tries to use default credential chain (IAM roles)
4. Error: "Unable to get IAM security credentials from EC2 Instance Metadata Service"

## Solution

Run the service with proper environment variable:

### PowerShell
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project gearify-catalog-svc
```

### Bash/Linux
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project gearify-catalog-svc
```

## Verify Configuration is Loading

When the service starts, you should see these diagnostic messages:

```
=== LocalStack Configuration ===
UseLocalStack: True
LocalStackHost: localhost:4566
AWS Region: us-east-1
Environment: Development
================================
AWS Options - Region: us-east-1
AWS Options - ServiceURL: http://localhost:4566
AWS services registered successfully
```

## If You See This Instead

```
=== LocalStack Configuration ===
UseLocalStack: False
LocalStackHost:
AWS Region: us-east-1
Environment: Production  <-- WRONG!
================================
```

**This means** `ASPNETCORE_ENVIRONMENT` was not set before running the service.

## Test the Service

Once running with correct environment:

### 1. Health Check
```bash
curl http://localhost:5000/health
```

Expected: `{"status":"healthy","service":"catalog"}`

### 2. Get All Products (requires X-Tenant-Id header)
```bash
curl http://localhost:5000/api/catalog/products -H "X-Tenant-Id: tenant1"
```

Expected: JSON array of products

### 3. Swagger UI
```
http://localhost:5000/swagger
```

## Common Mistakes

### ❌ Wrong: Running without setting environment
```powershell
dotnet run --project gearify-catalog-svc
```

### ✅ Correct: Set environment BEFORE running
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project gearify-catalog-svc
```

### ❌ Wrong: Setting environment in different terminal session
If you set the environment variable in one PowerShell window and run the service in another, the service won't see the variable.

### ✅ Correct: Set and run in same session
```powershell
# In the SAME PowerShell window:
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project gearify-catalog-svc
```

## Docker Compose Alternative

If you prefer using Docker Compose, the environment is set automatically:

```yaml
catalog-service:
  environment:
    - ASPNETCORE_ENVIRONMENT=Development  # <-- Set here
    - ASPNETCORE_URLS=http://+:8080
```

## Verification Steps

1. **Stop the currently running service** (the one showing internal server error)
2. **Open a new PowerShell window** in C:\Gearify
3. **Set environment variable:**
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Development"
   ```
4. **Verify it's set:**
   ```powershell
   echo $env:ASPNETCORE_ENVIRONMENT
   # Should output: Development
   ```
5. **Run the service:**
   ```powershell
   dotnet run --project gearify-catalog-svc
   ```
6. **Watch the console output** for the diagnostic messages
7. **Test the endpoint** with curl or Postman

## Expected Diagnostic Output

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

If you see this output, LocalStack is configured correctly and the service should work.

## If Problem Persists

Check these files to ensure LocalStack configuration is correct:

### 1. appsettings.Development.json
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

### 2. Verify LocalStack.Client.Extensions is installed
```bash
dotnet list gearify-catalog-svc/Gearify.CatalogService.csproj package | findstr LocalStack
```

Expected output:
```
LocalStack.Client.Extensions    1.4.0
```

## Table Information

Current state of gearify-products table:
- **Status:** ACTIVE
- **Items:** 17
- **Partition Key:** PK (String)
- **Sort Key:** SK (String)
- **GSI:** GSI1 (GSI1PK, GSI1SK)

## Quick Test Script

Save this as `test-catalog.ps1`:

```powershell
# Set environment
$env:ASPNETCORE_ENVIRONMENT="Development"

# Run service in background
$process = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd C:\Gearify; dotnet run --project gearify-catalog-svc" -PassThru

# Wait for service to start
Start-Sleep -Seconds 5

# Test health endpoint
Write-Host "Testing health endpoint..."
curl http://localhost:5000/health

# Test products endpoint
Write-Host "`nTesting products endpoint..."
curl http://localhost:5000/api/catalog/products -H "X-Tenant-Id: tenant1"

Write-Host "`nService is running in PID: $($process.Id)"
Write-Host "Press Enter to stop the service..."
Read-Host
Stop-Process -Id $process.Id
```
