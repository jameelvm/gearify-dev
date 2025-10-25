# Local Debug Setup Guide for Auth Service

This guide explains how to debug the Gearify Auth Service locally in your IDE while keeping other services running in Docker.

## Overview

When debugging locally:
- Auth service runs on your machine (port 5011)
- LocalStack and other services run in Docker
- API Gateway routes auth requests to your local instance
- Angular web app continues to work normally

## Prerequisites

- Docker containers running (except auth-svc which is stopped)
- Visual Studio 2022, JetBrains Rider, or VS Code
- .NET 8 SDK installed

## Configuration Files Updated

The following configuration files have been updated for local debugging:

### 1. `appsettings.Development.json`
Updated to work for local debugging:
- LocalStack host set to `localhost:4566` (accessible from your machine)
- AWS service URL set to `http://localhost:4566`
- Debug logging level enabled
- Works for local debugging since LocalStack port 4566 is mapped to your host

### 2. `Properties/launchSettings.json`
Added a new launch profile called **"Local Debug"** that:
- Sets environment to `Development`
- Runs on `http://localhost:5011`
- Configures AWS environment variables
- Opens Swagger UI automatically

### 3. `gearify-api-gateway/appsettings.Development.json`
Updated to route auth requests to `http://host.docker.internal:5011` when debugging locally

## Step-by-Step Instructions

### Step 1: Stop the Dockerized Auth Service

The auth service container is already stopped. If you need to stop it again:

```bash
cd C:\Gearify\gearify-umbrella
docker compose stop auth-svc
```

### Step 2: Choose Your IDE

#### Option A: Visual Studio 2022

1. Open `C:\Gearify\gearify-auth-svc\Gearify.AuthService.sln` in Visual Studio
2. In the toolbar, select the **"Local Debug"** profile from the dropdown
3. Press **F5** or click the "Start Debugging" button
4. Swagger UI will open at `http://localhost:5011/swagger`

#### Option B: JetBrains Rider

1. Open `C:\Gearify\gearify-auth-svc` folder in Rider
2. Go to **Run → Edit Configurations**
3. Click **+** and select **.NET Launch Settings Profile**
4. Select **"Local Debug"** from the profile dropdown
5. Click **OK**
6. Press **F5** or click the "Run" button
7. Swagger UI will open at `http://localhost:5011/swagger`

#### Option C: VS Code

1. Open `C:\Gearify\gearify-auth-svc` folder in VS Code
2. Create `.vscode/launch.json` if it doesn't exist:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Local Debug",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/bin/Debug/net8.0/Gearify.AuthService.dll",
      "args": [],
      "cwd": "${workspaceFolder}",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)",
        "uriFormat": "%s/swagger"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Local",
        "ASPNETCORE_URLS": "http://localhost:5011",
        "AWS_ACCESS_KEY_ID": "test",
        "AWS_SECRET_ACCESS_KEY": "test",
        "AWS_REGION": "us-east-1"
      }
    }
  ]
}
```

3. Create `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/Gearify.AuthService.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

4. Press **F5** to start debugging

### Step 3: Verify the Setup

Once the auth service is running locally, verify it's working:

#### Test 1: Check Swagger UI
- Navigate to `http://localhost:5011/swagger`
- You should see the Auth Service API documentation

#### Test 2: Check Health Endpoint
```bash
curl http://localhost:5011/api/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": []
}
```

#### Test 3: Test Login Through API Gateway
```bash
curl -X POST http://localhost:4200/api/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d '{
    "email": "test@example.com",
    "password": "Test1234"
  }'
```

Expected response:
```json
{
  "token": "eyJhbGci...",
  "refreshToken": "...",
  "user": {
    "id": "...",
    "email": "test@example.com",
    "firstName": "Test",
    "lastName": "User"
  }
}
```

#### Test 4: Test Login Through Web UI
1. Open browser to `http://localhost:4200`
2. Click "Login"
3. Enter credentials:
   - Email: `test@example.com`
   - Password: `Test1234`
4. Login should work normally

### Step 4: Set Breakpoints and Debug

You can now set breakpoints in your IDE:

**Common places to set breakpoints:**

1. **Controllers** (`API/Controllers/AuthController.cs`):
   - Line with `[HttpPost("login")]` method
   - Line with `[HttpPost("register")]` method

2. **Handlers** (`Application/Features/Auth/Handlers/`):
   - `LoginCommandHandler.cs` - line with password verification
   - `RegisterCommandHandler.cs` - line with user creation

3. **Repositories** (`Infrastructure/Repositories/UserRepository.cs`):
   - `GetByEmailAsync` method
   - `CreateAsync` method

**Example debugging flow:**

1. Set breakpoint in `LoginCommandHandler.Handle()` method
2. Trigger login from web UI or curl
3. Execution will pause at your breakpoint
4. Inspect variables:
   - `request.Email`
   - `user` object
   - `passwordValid` result
5. Step through code with F10/F11
6. Watch the JWT token being generated

## Architecture

```
┌─────────────────┐
│   Browser       │
│  localhost:4200 │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Nginx (Docker)│
│   Port 80       │
└────────┬────────┘
         │
         ▼
┌──────────────────┐
│ API Gateway      │
│ (Docker)         │
│ Port 8080        │  ← ASPNETCORE_ENVIRONMENT=Local
└────────┬─────────┘    (reads appsettings.Local.json)
         │
         │  Routes /api/auth/* to
         │  http://host.docker.internal:5011
         │
         ▼
┌──────────────────────────┐
│  Auth Service            │
│  (Your IDE - Local)      │  ← You debug here!
│  localhost:5011          │
└────────┬─────────────────┘
         │
         ▼
┌──────────────────────────┐
│  LocalStack (Docker)     │
│  localhost:4566          │
│  - DynamoDB              │
│  - S3, SQS, SNS, etc.    │
└──────────────────────────┘
```

## Important Notes

### Network Configuration

- **`host.docker.internal`**: Special DNS name that Docker uses to reach the host machine
  - API Gateway (in Docker) uses this to call your local auth service
  - Resolves to your host machine's IP address

- **`localhost:4566`**: LocalStack endpoint
  - Your local auth service connects to LocalStack on port 4566
  - Works because LocalStack port is mapped to host: `4566:4566`

### Environment Variables

The `Local Debug` profile sets these environment variables:

```bash
ASPNETCORE_ENVIRONMENT=Local        # Loads appsettings.Local.json
ASPNETCORE_URLS=http://localhost:5011
AWS_ACCESS_KEY_ID=test             # LocalStack test credentials
AWS_SECRET_ACCESS_KEY=test         # LocalStack test credentials
AWS_REGION=us-east-1
```

### Configuration Hierarchy

ASP.NET Core loads configuration in this order:
1. `appsettings.json` (base)
2. `appsettings.Local.json` (environment-specific)
3. Environment variables (highest priority)

## Troubleshooting

### Issue 1: "Connection refused" to LocalStack

**Problem**: Auth service can't connect to DynamoDB
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

### Issue 2: API Gateway can't reach local auth service

**Problem**: Login fails with 502 Bad Gateway

**Solution**:
1. Verify auth service is running on port 5011:
   ```bash
   curl http://localhost:5011/api/health
   ```
2. Check API Gateway logs:
   ```bash
   docker logs gearify-api-gateway
   ```
3. Verify API Gateway is using `Local` environment:
   ```bash
   docker exec gearify-api-gateway env | grep ASPNETCORE_ENVIRONMENT
   ```
   Should show: `ASPNETCORE_ENVIRONMENT=Local`

### Issue 3: Wrong configuration file being loaded

**Problem**: Service uses wrong LocalStack host (e.g., `localstack:4566` instead of `localhost:4566`)

**Solution**:
1. Verify `ASPNETCORE_ENVIRONMENT` is set to `Local`:
   - Check your IDE's run configuration
   - Check the environment variable in launch profile
2. Add logging to see which config is loaded:
   ```csharp
   var localStackHost = builder.Configuration["LocalStack:Config:LocalStackHost"];
   Console.WriteLine($"LocalStack Host: {localStackHost}");
   ```

### Issue 4: Firewall blocking connections

**Problem**: Connection timeouts or "No connection could be made" errors

**Solution**:
1. Allow .NET app through Windows Firewall
2. Or temporarily disable firewall for testing
3. Check if antivirus is blocking the connection

### Issue 5: Port 5011 already in use

**Problem**:
```
Unable to bind to http://localhost:5011: address already in use
```

**Solution**:
1. Find process using port 5011:
   ```powershell
   netstat -ano | findstr :5011
   ```
2. Kill the process or change port in `launchSettings.json`

## Switching Back to Docker

When you're done debugging locally:

### Step 1: Stop local auth service
Press the "Stop" button in your IDE or Ctrl+C

### Step 2: Restart dockerized auth service
```bash
cd C:\Gearify\gearify-umbrella
docker compose start auth-svc
```

### Step 3: Verify API Gateway routes back to Docker auth service
The API Gateway will automatically route to the dockerized auth-svc when it's running. No configuration changes needed!

## Tips for Effective Debugging

### 1. Use Conditional Breakpoints
Right-click breakpoint → Conditions
```csharp
// Only break when email is specific value
request.Email == "test@example.com"
```

### 2. Watch Key Variables
Add to Watch window:
- `request.Email`
- `user.PasswordHash`
- `passwordValid`
- `token` (JWT)

### 3. Use Immediate Window
While debugging, execute code:
```csharp
BCrypt.Net.BCrypt.Verify("Test1234", user.PasswordHash)
```

### 4. Log Everything
Serilog is configured in the service. Use structured logging:
```csharp
_logger.LogInformation("User {Email} logged in from {IpAddress}",
    user.Email, context.Connection.RemoteIpAddress);
```

### 5. Monitor DynamoDB
Watch DynamoDB operations in real-time:
```bash
# Scan users table
docker exec gearify-localstack awslocal dynamodb scan \
  --table-name gearify-users

# Watch CloudWatch logs
docker exec gearify-localstack awslocal logs tail \
  /aws/dynamodb/gearify-users --follow
```

## Next Steps

After setting up local debugging:

1. **Add Unit Tests**: Write tests for handlers and repositories
2. **Add Integration Tests**: Test full authentication flows
3. **Profile Performance**: Use diagnostic tools to find bottlenecks
4. **Implement New Features**: Add password reset, email verification, etc.
5. **Refactor**: Improve code quality with local debugging safety net

## Configuration Reference

### appsettings.Development.json (Auth Service)
**Key settings for local debugging:**
- `LocalStackHost`: `"localhost:4566"` - connects to LocalStack on your host machine
- `ServiceURL`: `"http://localhost:4566"` - AWS SDK connects to LocalStack
- Logging level: `"Debug"` - shows detailed logs while debugging

This file is used both for local debugging and when running in Docker (since LocalStack port 4566 is accessible from both)

### Properties/launchSettings.json
```json
{
  "profiles": {
    "Local Debug": {
      "commandName": "Project",
      "launchBrowser": true,
      "launchUrl": "swagger",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "http://localhost:5011",
        "AWS_ACCESS_KEY_ID": "test",
        "AWS_SECRET_ACCESS_KEY": "test",
        "AWS_REGION": "us-east-1"
      },
      "applicationUrl": "http://localhost:5011"
    }
  }
}
```

## Summary

You now have a complete local debugging setup for the Gearify Auth Service:

✅ Auth service runs locally on port 5011
✅ LocalStack accessible from local service
✅ API Gateway routes to local instance
✅ Web UI works normally
✅ Full debugging capabilities in your IDE
✅ Easy to switch between local and Docker

Happy debugging! 🐛🔍
