# Auth Service - Local Debugging Instructions

## The Issue You Encountered

When you tried to run the auth service locally, the browser opened on port 5011 but then immediately closed. This happened because:

**Port 5011 was already in use by the Docker auth service container!**

The error in the logs showed:
```
Failed to bind to address http://[::1]:5011: address already in use.
```

## Solution: Stop Docker Auth Service First

Before debugging locally, you **MUST** stop the Docker auth service container:

```bash
cd C:\Gearify\gearify-umbrella
docker compose stop auth-svc
```

## Step-by-Step: Debug Auth Service Locally

### 1. Stop Docker Auth Service
```bash
cd C:\Gearify\gearify-umbrella
docker compose stop auth-svc
```

### 2. Verify Port 5011 is Free
```bash
netstat -ano | findstr :5011
```

If you see output, wait a few seconds for connections to close or restart your machine.

### 3. Open Auth Service in Your IDE

**Visual Studio:**
1. Open `C:\Gearify\gearify-auth-svc\Gearify.AuthService.sln`
2. Select **"Local Debug"** from the profile dropdown (next to the Start button)
3. Press **F5** or click "Start Debugging"
4. Browser should open to `http://localhost:5011/swagger`

**JetBrains Rider:**
1. Open `C:\Gearify\gearify-auth-svc` folder
2. Go to **Run → Edit Configurations**
3. Select **"Local Debug"** profile
4. Press **F5** or click the Debug button
5. Browser should open to `http://localhost:5011/swagger`

**VS Code:**
1. Open `C:\Gearify\gearify-auth-svc` folder
2. Open Terminal: `dotnet run --launch-profile "Local Debug"`
3. Open browser to `http://localhost:5011/swagger`

### 4. Verify It's Running

**Test 1: Check Swagger UI**
Open browser: `http://localhost:5011/swagger`

**Test 2: Test Health Endpoint**
```bash
curl http://localhost:5011/api/health -H "X-Tenant-Id: default"
```

**Test 3: Test Login Through API Gateway**
```bash
curl -X POST http://localhost:4200/api/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -d '{"email":"test@example.com","password":"Test1234"}'
```

Should return JWT token.

**Test 4: Test Login from Web UI**
1. Open `http://localhost:4200`
2. Click "Login"
3. Enter: `test@example.com` / `Test1234`
4. Should successfully log in

### 5. Set Breakpoints and Debug!

**Common breakpoint locations:**

1. **API Controllers** (`API/Controllers/AuthController.cs`):
   ```csharp
   [HttpPost("login")]
   public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginCommand command)
   {
       // SET BREAKPOINT HERE
       var result = await _mediator.Send(command);
       return Ok(result);
   }
   ```

2. **Command Handlers** (`Application/Features/Auth/Handlers/LoginCommandHandler.cs`):
   ```csharp
   public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
   {
       // SET BREAKPOINT HERE - Start of login logic
       var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

       // SET BREAKPOINT HERE - After user lookup
       if (user == null) { ... }

       // SET BREAKPOINT HERE - Before password verification
       var passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

       // SET BREAKPOINT HERE - After password check
       if (!passwordValid) { ... }
   }
   ```

3. **Repository** (`Infrastructure/Repositories/UserRepository.cs`):
   ```csharp
   public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
   {
       // SET BREAKPOINT HERE - DynamoDB query
       var response = await _dynamoDb.QueryAsync(request, cancellationToken);
       return user;
   }
   ```

### 6. Debug Flow Example

1. Set breakpoint in `LoginCommandHandler.Handle()` method
2. Trigger login from web UI: `http://localhost:4200`
3. Execution pauses at your breakpoint
4. Inspect variables:
   - `request.Email` - should be "test@example.com"
   - `user` - after database lookup
   - `passwordValid` - true/false
   - `token` - generated JWT
5. Press F10 to step over, F11 to step into
6. Continue debugging through the flow

## When You're Done Debugging

### Stop Local Auth Service
Press the "Stop" button in your IDE or press `Ctrl+C` in terminal

### Restart Docker Auth Service
```bash
cd C:\Gearify\gearify-umbrella
docker compose start auth-svc
```

### Verify Docker Service is Running
```bash
docker ps | grep auth-svc
```

Should show the container running.

## Troubleshooting

### Problem: Port still in use after stopping Docker
```bash
# Find process using port 5011
netstat -ano | findstr :5011

# Note the PID (last column) and kill it
taskkill /PID <pid> /F
```

### Problem: Can't connect to LocalStack
```bash
# Check LocalStack is running
docker ps | grep localstack

# Check LocalStack health
curl http://localhost:4566/_localstack/health
```

### Problem: API Gateway not routing to local service
```bash
# Check API Gateway logs
docker logs gearify-api-gateway --tail 50

# Verify it's using Development environment (should see routing to host.docker.internal:5011)
```

### Problem: DynamoDB table not found
```bash
# List DynamoDB tables
docker exec gearify-localstack awslocal dynamodb list-tables

# If gearify-users doesn't exist, check LocalStack init scripts
docker logs gearify-localstack | grep dynamodb
```

## Configuration Summary

The following files have been configured for local debugging:

1. **`appsettings.Development.json`** - Points to `localhost:4566` for LocalStack
2. **`Properties/launchSettings.json`** - "Local Debug" profile on port 5011
3. **`gearify-api-gateway/appsettings.Development.json`** - Routes auth to `host.docker.internal:5011`

## Why This Setup Works

```
┌─────────────┐
│   Browser   │
│ :4200       │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Nginx     │
│ (Docker)    │
└──────┬──────┘
       │
       ▼
┌──────────────┐
│ API Gateway  │
│ (Docker)     │ ← Routes /api/auth/* to host.docker.internal:5011
└──────┬───────┘
       │
       │ host.docker.internal = your Windows machine
       │
       ▼
┌────────────────────┐
│   Auth Service     │
│   (Your IDE)       │ ← YOU DEBUG HERE! 🐛
│   localhost:5011   │
└─────────┬──────────┘
          │
          ▼
┌─────────────────────┐
│   LocalStack        │
│   (Docker)          │
│   localhost:4566    │
│   - DynamoDB        │
└─────────────────────┘
```

**Key points:**
- `host.docker.internal` is a special Docker hostname that resolves to your Windows host machine
- LocalStack port 4566 is mapped to host, so `localhost:4566` works from your machine
- API Gateway runs in Docker but routes to your local auth service
- All other services continue running in Docker normally

## Summary

✅ **Stop Docker auth service first**: `docker compose stop auth-svc`
✅ **Run from IDE**: Select "Local Debug" profile and press F5
✅ **Set breakpoints**: Controllers, handlers, repositories
✅ **Test**: Web UI at http://localhost:4200 works normally
✅ **When done**: Restart Docker auth service

Happy debugging! 🎉
