# Quick Start: Local Debugging

## TL;DR

To debug the auth service locally:

1. **Stop the Docker auth service:**
   ```bash
   cd C:\Gearify\gearify-umbrella
   docker compose stop auth-svc
   ```

2. **Open auth service in your IDE:**
   - Visual Studio: Open `Gearify.AuthService.sln`
   - Rider: Open the `gearify-auth-svc` folder
   - VS Code: Open the `gearify-auth-svc` folder

3. **Select "Local Debug" profile and press F5**

4. **Test it:**
   - Swagger: http://localhost:5011/swagger
   - Web UI: http://localhost:4200 (login works normally)

That's it! 🎉

## Why This Works

We updated `appsettings.Development.json` to use `localhost:4566` instead of `localstack:4566` for LocalStack. This means:

- ✅ Works when running locally (your machine can access `localhost:4566`)
- ✅ Works when running in Docker (LocalStack port 4566 is mapped to host)
- ✅ No need for separate config files
- ✅ Simple and straightforward

## What Was Changed

### 1. `appsettings.Development.json`
```json
{
  "LocalStack": {
    "Config": {
      "LocalStackHost": "localhost:4566"  // Changed from "localstack:4566"
    }
  },
  "AWS": {
    "ServiceURL": "http://localhost:4566"  // Changed from "http://localstack:4566"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"  // Changed from "Information" for better debugging
    }
  }
}
```

### 2. `Properties/launchSettings.json`
Added "Local Debug" profile that runs on port 5011 with Development environment.

### 3. `appsettings.Development.json` (API Gateway)
Added routing override to send auth requests to `http://host.docker.internal:5011` (your local machine).

## Common Questions

**Q: Do I need to change anything when switching between Docker and local debugging?**

A: No! Just stop the Docker auth service when debugging locally, and start it again when done.

**Q: What about other developers? Will this affect them?**

A: No! The configuration works for both scenarios:
- Running in Docker: Auth service uses `localhost:4566` which resolves correctly
- Running locally: Your machine uses `localhost:4566` which also works

**Q: Do I need to rebuild Docker containers?**

A: No! The API Gateway is already configured to route to your local instance.

**Q: Where should I set breakpoints?**

A: Common places:
- `API/Controllers/AuthController.cs` - API endpoints
- `Application/Features/Auth/Handlers/LoginCommandHandler.cs` - Login logic
- `Infrastructure/Repositories/UserRepository.cs` - Database operations

## When You're Done Debugging

```bash
# Stop local auth service (press Stop in IDE)

# Restart Docker auth service
docker compose start auth-svc
```

Everything will work as before!

## Full Documentation

For complete details, troubleshooting, and advanced tips, see:
**LOCAL_DEBUG_SETUP.md**
