# Local Debug Setup Guide for Catalog Service

This guide explains how to debug the Gearify Catalog Service locally in your IDE while keeping other services running in Docker.

## Overview

When debugging locally:
- Catalog service runs on your machine (port 5001)
- LocalStack and other services run in Docker
- API Gateway routes catalog requests to your local instance
- Angular web app continues to work normally

## Prerequisites

- Docker containers running (except catalog-svc which is stopped)
- Visual Studio 2022, JetBrains Rider, or VS Code
- .NET 8 SDK installed

## Configuration Files Updated

The following configuration files have been configured for local debugging:

### 1. `Properties/launchSettings.json`
Added a new launch profile called **"Local Debug"** that:
- Sets environment to `Development`
- Runs on `http://localhost:5001`
- Configures AWS environment variables
- Opens Swagger UI automatically

### 2. `appsettings.Development.json`
Configured to work for local debugging:
- LocalStack host set to `localhost:4566` (accessible from your machine)
- AWS service URL set to `http://localhost:4566`
- Works for local debugging since LocalStack port 4566 is mapped to your host

### 3. `gearify-api-gateway/appsettings.Development.json`
Updated to route catalog requests to `http://host.docker.internal:5001` when debugging locally

## Step-by-Step Instructions

### Step 1: Stop the Dockerized Catalog Service

```bash
cd C:\Gearify\gearify-umbrella
docker compose stop catalog-svc
```

### Step 2: Start the Dockerized Auth Service

Make sure auth-svc is running in Docker (not locally):

```bash
cd C:\Gearify\gearify-umbrella
docker compose start auth-svc
```

### Step 3: Restart API Gateway

The API Gateway needs to reload configuration to route catalog to localhost:

```bash
cd C:\Gearify\gearify-umbrella
docker compose restart api-gateway
```

Wait a few seconds for it to start, then verify:
```bash
docker logs gearify-api-gateway --tail 20
```

### Step 4: Choose Your IDE

#### Option A: Visual Studio 2022

1. Open `C:\Gearify\gearify-catalog-svc\Gearify.CatalogService.sln` in Visual Studio
2. In the toolbar, select the **"Local Debug"** profile from the dropdown
3. Press **F5** or click the "Start Debugging" button
4. Swagger UI will open at `http://localhost:5001/swagger`

#### Option B: JetBrains Rider

1. Open `C:\Gearify\gearify-catalog-svc` folder in Rider
2. Go to **Run → Edit Configurations**
3. Click **+** and select **.NET Launch Settings Profile**
4. Select **"Local Debug"** from the profile dropdown
5. Click **OK**
6. Press **F5** or click the "Run" button
7. Swagger UI will open at `http://localhost:5001/swagger`

#### Option C: VS Code

1. Open `C:\Gearify\gearify-catalog-svc` folder in VS Code
2. VS Code configuration files (`.vscode/launch.json` and `.vscode/tasks.json`) are already created
3. Press **F5** to start debugging
4. Swagger UI will open at `http://localhost:5001/swagger`

### Step 5: Verify the Setup

Once the catalog service is running locally, verify it's working:

#### Test 1: Check Swagger UI
- Navigate to `http://localhost:5001/swagger`
- You should see the Catalog Service API documentation

#### Test 2: Check Health Endpoint
```bash
curl http://localhost:5001/health
```

Expected response:
```json
{
  "status": "healthy",
  "service": "catalog"
}
```

#### Test 3: Test Categories Through API Gateway
```bash
curl -H "X-Tenant-ID: default" http://localhost:8080/api/catalog/categories/mega-menu
```

Expected response: JSON array of categories with sections and subcategories

#### Test 4: Test Products Through API Gateway
```bash
curl -H "X-Tenant-ID: default" http://localhost:8080/api/catalog/products
```

Expected response: JSON array of products

#### Test 5: Test Through Web UI
1. Open browser to `http://localhost:4200`
2. The category navigation should load from your local catalog service
3. You should see categories in the navigation bar

### Step 6: Set Breakpoints and Debug

You can now set breakpoints in your IDE:

**Common places to set breakpoints:**

1. **Controllers** (`API/Controllers/`):
   - `CategoriesController.cs` - `GetMegaMenuData()` method
   - `ProductsController.cs` - `GetProducts()` method

2. **Query Handlers** (`Application/Queries/`):
   - `GetAllCategoriesQueryHandler.cs`
   - `GetCategoryWithDetailsQueryHandler.cs`
   - `GetProductsQueryHandler.cs`

3. **Repositories** (`Infrastructure/Repositories/`):
   - `DynamoDbCategoryRepository.cs` - `GetAllCategoriesAsync()` method
   - `DynamoDbProductRepository.cs` - `GetProductsAsync()` method

**Example debugging flow:**

1. Set breakpoint in `CategoriesController.GetMegaMenuData()` method
2. Refresh the web UI page or call the API from curl
3. Execution will pause at your breakpoint
4. Inspect variables:
   - `categories` list
   - `sections` and `subcategories` data
5. Step through code with F10/F11
6. Watch DynamoDB queries being executed

## Architecture

```
┌─────────────────┐
│   Browser       │
│  localhost:4200 │
└────────┬────────┘
         │
         ▼
┌──────────────────┐
│ API Gateway      │
│ (Docker)         │
│ Port 8080        │  ← ASPNETCORE_ENVIRONMENT=Development
└────────┬─────────┘    (reads appsettings.Development.json)
         │
         │  Routes /api/catalog/* to
         │  http://host.docker.internal:5001
         │
         ▼
┌──────────────────────────┐
│  Catalog Service         │
│  (Your IDE - Local)      │  ← You debug here!
│  localhost:5001          │
└────────┬─────────────────┘
         │
         ▼
┌──────────────────────────┐
│  LocalStack (Docker)     │
│  localhost:4566          │
│  - DynamoDB              │
│  - S3                    │
└──────────────────────────┘
```

## Important Notes

### Network Configuration

- **`host.docker.internal`**: Special DNS name that Docker uses to reach the host machine
  - API Gateway (in Docker) uses this to call your local catalog service
  - Resolves to your host machine's IP address

- **`localhost:4566`**: LocalStack endpoint
  - Your local catalog service connects to LocalStack on port 4566
  - Works because LocalStack port is mapped to host: `4566:4566`

### Environment Variables

The `Local Debug` profile sets these environment variables:

```bash
ASPNETCORE_ENVIRONMENT=Development     # Loads appsettings.Development.json
ASPNETCORE_URLS=http://localhost:5001
AWS_ACCESS_KEY_ID=test                # LocalStack test credentials
AWS_SECRET_ACCESS_KEY=test            # LocalStack test credentials
AWS_REGION=us-east-1
DYNAMODB_ENDPOINT=http://localhost:4566
S3_ENDPOINT=http://localhost:4566
```

### Configuration Hierarchy

ASP.NET Core loads configuration in this order:
1. `appsettings.json` (base)
2. `appsettings.Development.json` (environment-specific)
3. Environment variables (highest priority)

## Troubleshooting

### Issue 1: "Connection refused" to LocalStack

**Problem**: Catalog service can't connect to DynamoDB
```
Amazon.Runtime.AmazonServiceException: Unable to connect to endpoint http://localhost:4566
```

**Solution**:
1. Verify LocalStack is running:
   ```bash
   docker ps | grep localstack
   ```
2. Check LocalStack health:
   ```bash
   curl http://localhost:4566/_localstack/health
   ```

### Issue 2: API Gateway can't reach local catalog service

**Problem**: Catalog requests fail with 502 Bad Gateway

**Solution**:
1. Verify catalog service is running on port 5001:
   ```bash
   curl http://localhost:5001/health
   ```
2. Check API Gateway logs:
   ```bash
   docker logs gearify-api-gateway
   ```
3. Verify API Gateway routes are configured:
   ```bash
   docker exec gearify-api-gateway cat /app/appsettings.Development.json
   ```

### Issue 3: Categories not loading

**Problem**: Web UI shows empty categories or errors

**Solution**:
1. Check if catalog service is receiving requests:
   - Look at console output in your IDE
   - Set breakpoint in `CategoriesController`
2. Verify DynamoDB has seed data:
   ```bash
   docker exec gearify-localstack awslocal dynamodb scan --table-name gearify-catalog --max-items 5
   ```
3. Check tenant header is being sent:
   ```bash
   curl -v -H "X-Tenant-ID: default" http://localhost:5001/api/catalog/categories
   ```

### Issue 4: Port 5001 already in use

**Problem**:
```
Unable to bind to http://localhost:5001: address already in use
```

**Solution**:
1. Find process using port 5001:
   ```powershell
   netstat -ano | findstr :5001
   ```
2. Kill the process or change port in `launchSettings.json`

## Switching Back to Docker

When you're done debugging locally:

### Step 1: Stop local catalog service
Press the "Stop" button in your IDE or Ctrl+C

### Step 2: Restart dockerized catalog service
```bash
cd C:\Gearify\gearify-umbrella
docker compose start catalog-svc
```

### Step 3: Restart API Gateway
```bash
docker compose restart api-gateway
```

The API Gateway will automatically route back to the dockerized catalog-svc.

## Tips for Effective Debugging

### 1. Use Conditional Breakpoints
Right-click breakpoint → Conditions
```csharp
// Only break when specific category is requested
categoryId == "cat_bats"
```

### 2. Watch Key Variables
Add to Watch window:
- `tenantId`
- `categories` list
- `sections` and `subcategories`
- DynamoDB query parameters

### 3. Use Immediate Window
While debugging, execute code:
```csharp
categories.Count
categories.First().Name
```

### 4. Log Everything
Serilog is configured in the service. Use structured logging:
```csharp
_logger.LogInformation("Loading categories for tenant {TenantId}", tenantId);
```

### 5. Monitor DynamoDB
Watch DynamoDB operations in real-time:
```bash
# Scan catalog table
docker exec gearify-localstack awslocal dynamodb scan \
  --table-name gearify-catalog --max-items 10

# Query specific category
docker exec gearify-localstack awslocal dynamodb query \
  --table-name gearify-catalog \
  --key-condition-expression "PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"TENANT#default#CATEGORY#cat_bats"}}'
```

## Common Debugging Scenarios

### Scenario 1: Debug Category Loading
1. Set breakpoint in `CategoriesController.GetMegaMenuData()`
2. Refresh web UI or call API
3. Step through:
   - MediatR sends `GetAllCategoriesQuery`
   - Handler calls repository
   - Repository queries DynamoDB
   - Data transformed to DTOs

### Scenario 2: Debug Product Queries
1. Set breakpoint in `ProductsController.GetProducts()`
2. Navigate to products page or call API
3. Inspect:
   - Query parameters (filters, pagination)
   - DynamoDB scan/query operations
   - Product transformation logic

### Scenario 3: Debug Tenant Isolation
1. Set breakpoint in tenant middleware
2. Make request with different tenant headers
3. Verify:
   - Correct tenant ID extracted
   - Queries scoped to tenant
   - Data isolation working

## Summary

You now have a complete local debugging setup for the Gearify Catalog Service:

✅ Catalog service runs locally on port 5001
✅ LocalStack accessible from local service
✅ API Gateway routes to local instance
✅ Web UI works normally
✅ Full debugging capabilities in your IDE
✅ Easy to switch between local and Docker

Happy debugging! 🐛🔍
