# Verify Debug Setup

This document helps you verify that the local debug setup is configured correctly.

## Current Configuration

### Services Running in Docker (Container Mode)
✅ **Auth Service** - Running at `http://auth-svc:80` (inside Docker network)
  - Accessible from host: `http://localhost:5011`
  - API Gateway routes: `/api/auth/*` → `http://auth-svc:80`

✅ **All other services** - Running in Docker

### Service Ready for Local Debug
🔧 **Catalog Service** - Ready to run locally at `http://localhost:5001`
  - API Gateway routes: `/api/catalog/*` → `http://host.docker.internal:5001`
  - Launch profile: "Local Debug" configured

## Quick Verification

### Step 1: Verify Containers
```bash
cd C:\Gearify\gearify-umbrella
docker compose ps
```

Expected output:
- ✅ auth-svc: Running
- ❌ catalog-svc: Stopped (you'll run this locally)
- ✅ api-gateway: Running
- ✅ localstack: Running
- ✅ Other services: Running

### Step 2: Test Auth Service (Containerized)
```bash
# Direct access to container
curl http://localhost:5011/health

# Through API Gateway
curl http://localhost:8080/api/auth/health
```

Both should return: `{"status":"healthy","service":"auth"}`

### Step 3: Start Catalog Service Locally

#### Using VS Code:
1. Open `C:\Gearify\gearify-catalog-svc` in VS Code
2. Press **F5** (or Run → Start Debugging)
3. Select "Local Debug" profile
4. Swagger should open at `http://localhost:5001/swagger`

#### Using Visual Studio 2022:
1. Open `C:\Gearify\gearify-catalog-svc\Gearify.CatalogService.sln`
2. Select "Local Debug" from profile dropdown
3. Press **F5**
4. Swagger should open at `http://localhost:5001/swagger`

#### Using Rider:
1. Open `C:\Gearify\gearify-catalog-svc` in Rider
2. Select "Local Debug" run configuration
3. Press **F5**
4. Swagger should open at `http://localhost:5001/swagger`

### Step 4: Test Catalog Service (Local)
```bash
# Direct access to local service
curl http://localhost:5001/health

# Through API Gateway (will route to your local instance)
curl -H "X-Tenant-ID: default" http://localhost:8080/api/catalog/categories/mega-menu
```

The first should return: `{"status":"healthy","service":"catalog"}`
The second should return: JSON array of categories

### Step 5: Test Through Web UI
1. Open browser: `http://localhost:4200`
2. Categories should load in navigation (from your local catalog service)
3. Login should work (from containerized auth service)

## Debugging Tips

### Set Breakpoints in Catalog Service
1. Open `CategoriesController.cs` in your IDE
2. Set breakpoint on line with `GetMegaMenuData()` method
3. Refresh web UI page
4. Breakpoint should hit - you're debugging!

### Monitor Logs
```bash
# API Gateway logs
docker logs -f gearify-api-gateway

# Auth Service logs (container)
docker logs -f gearify-auth-svc

# LocalStack logs
docker logs -f gearify-localstack
```

## Troubleshooting

### Catalog service won't start on port 5001
**Problem**: Port already in use

**Solution**:
```powershell
netstat -ano | findstr :5001
# Kill the process or change port in launchSettings.json
```

### API Gateway returns 502 Bad Gateway for catalog
**Problem**: API Gateway can't reach local catalog service

**Solution**:
1. Verify catalog is running: `curl http://localhost:5001/health`
2. Check API Gateway config:
   ```bash
   docker exec gearify-api-gateway cat /app/appsettings.Development.json
   ```
   Should show: `"Address": "http://host.docker.internal:5001"`
3. Restart API Gateway:
   ```bash
   docker compose restart api-gateway
   ```

### Catalog can't connect to LocalStack
**Problem**: Connection refused to localhost:4566

**Solution**:
1. Verify LocalStack is running:
   ```bash
   docker ps | grep localstack
   curl http://localhost:4566/_localstack/health
   ```
2. Check environment variables in launch profile
3. Verify DYNAMODB_ENDPOINT=http://localhost:4566

## Switching Services

### To debug Auth instead of Catalog:
1. Stop local catalog service (Ctrl+C or Stop button)
2. Start catalog container:
   ```bash
   docker compose start catalog-svc
   ```
3. Stop auth container:
   ```bash
   docker compose stop auth-svc
   ```
4. Update `gearify-api-gateway/appsettings.Development.json`:
   - Change auth to: `"Address": "http://host.docker.internal:5011"`
   - Change catalog to: `"Address": "http://catalog-svc:80"`
5. Restart API Gateway:
   ```bash
   docker compose restart api-gateway
   ```
6. Start auth service locally from IDE

### To run everything in Docker:
1. Stop all local services
2. Start all containers:
   ```bash
   cd C:\Gearify\gearify-umbrella
   docker compose start auth-svc catalog-svc
   docker compose restart api-gateway
   ```

## Summary

Your debug setup is configured as follows:

| Service | Mode | URL | Notes |
|---------|------|-----|-------|
| Auth | Container | http://localhost:5011 | Running in Docker |
| Catalog | **Local Debug** | http://localhost:5001 | Run from your IDE |
| API Gateway | Container | http://localhost:8080 | Routes to correct services |
| LocalStack | Container | http://localhost:4566 | Shared by all services |
| Web UI | Dev Server | http://localhost:4200 | Angular dev server |

✅ Ready to debug Catalog Service!
📖 See: `gearify-catalog-svc/LOCAL_DEBUG_SETUP.md` for detailed guide
