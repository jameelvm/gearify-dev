# Debug Guide - Quick Reference

## Important: API Gateway Routing

**The API Gateway does NOT automatically switch between Docker and local endpoints.**

You must manually tell it where to route requests. See `API_GATEWAY_ROUTING.md` for details.

**Quick version:**
- Docker mode: API Gateway → `auth-svc:80`
- Local debug: You must change config to → `host.docker.internal:5011`

---

## Debug Any Service Locally

### Step 1: Update API Gateway Config (Auth Service Only)

**File:** `C:\Gearify\gearify-api-gateway\appsettings.Development.json`

Change this:
```json
"auth-cluster": {
  "Destinations": {
    "auth-destination": {
      "Address": "http://auth-svc:80"
    }
  }
}
```

To this:
```json
"auth-cluster": {
  "Destinations": {
    "auth-destination": {
      "Address": "http://host.docker.internal:5011"
    }
  }
}
```

Then restart API Gateway:
```bash
docker compose restart api-gateway
```

### Step 2: Stop Docker Service
```bash
cd C:\Gearify\gearify-umbrella
docker compose stop <service-name>
```

**Service names:**
- `auth-svc` - Authentication service
- `catalog-svc` - Catalog service
- `cart-svc` - Cart service
- `order-svc` - Order service
- `payment-svc` - Payment service
- etc.

### Step 3: Open Service in IDE
- Visual Studio: Open the `.sln` file
- Rider: Open the service folder
- VS Code: Open the service folder

### Step 4: Run with Debug Profile
- Select the debug profile (usually service name or "Local Debug")
- Press **F5**
- Browser opens with Swagger

### Step 5: Set Breakpoints
- Click in the left margin next to any line of code
- Red dot appears = breakpoint set

### Step 6: Test
- Use web UI at `http://localhost:4200`
- Or use Swagger at the local port
- Execution will pause at your breakpoints

---

## Switch Back to Docker

### Step 1: Stop Local Service
- Press **Stop** button in IDE
- Or press `Ctrl+C` in terminal

### Step 2: Change API Gateway Config Back (Auth Service Only)

**File:** `C:\Gearify\gearify-api-gateway\appsettings.Development.json`

Change back to:
```json
"auth-cluster": {
  "Destinations": {
    "auth-destination": {
      "Address": "http://auth-svc:80"
    }
  }
}
```

Restart API Gateway:
```bash
docker compose restart api-gateway
```

### Step 3: Restart Docker Service
```bash
cd C:\Gearify\gearify-umbrella
docker compose start <service-name>
```

### Step 4: Verify
```bash
docker ps | grep <service-name>
```

Should show container running.

---

## Common Examples

### Debug Auth Service

**Start debugging:**
```bash
# 1. Edit appsettings.Development.json - change Address to host.docker.internal:5011
# 2. docker compose restart api-gateway
# 3. docker compose stop auth-svc
# 4. Open C:\Gearify\gearify-auth-svc in IDE
# 5. Select "auth-svc" profile
# 6. Press F5
```

**Switch back:**
```bash
# 1. Stop in IDE (press Stop button)
# 2. Edit appsettings.Development.json - change Address to auth-svc:80
# 3. docker compose restart api-gateway
# 4. docker compose start auth-svc
```

---

### Debug Multiple Services

**Debug auth + catalog together:**
```bash
# 1. Stop both
docker compose stop auth-svc catalog-svc

# 2. Open each service in separate IDE windows
# 3. Press F5 in both
```

**Switch back:**
```bash
docker compose start auth-svc catalog-svc
```

---

## Troubleshooting

### Port Already in Use

**Problem:** Service won't start, says port in use.

**Solution:**
```bash
# Find what's using the port (example: 5011)
netstat -ano | findstr :5011

# Kill the process (replace XXXX with PID from above)
powershell -Command "Stop-Process -Id XXXX -Force"
```

### Docker Service Won't Stop

**Problem:** `docker compose stop` doesn't work.

**Solution:**
```bash
# Force remove container
docker rm -f gearify-<service-name>

# Example
docker rm -f gearify-auth-svc
```

### Web UI Not Working

**Problem:** Web UI shows errors after switching to local debug.

**Solution:**
- Make sure API Gateway is still running in Docker
- Check service is actually running locally
- Clear browser cache and refresh

---

## Port Reference

Each service runs on a specific port when debugging locally:

| Service | Docker Port | Local Debug Port |
|---------|------------|------------------|
| Auth | 5011 | 5011 |
| Catalog | TBD | TBD |
| Cart | TBD | TBD |
| Order | TBD | TBD |
| Payment | TBD | TBD |

---

## Quick Commands

```bash
# Stop all services
docker compose down

# Start all services
docker compose up -d

# View logs of a service
docker logs gearify-<service-name>

# Check which services are running
docker ps
```

---

## Summary

**To Debug:**
1. `docker compose stop <service-name>`
2. Open in IDE
3. Press F5

**To Switch Back:**
1. Stop in IDE
2. `docker compose start <service-name>`

That's it! 🎉
