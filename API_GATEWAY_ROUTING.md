# API Gateway Routing - How It Works

## Simple Answer

**The API Gateway ALWAYS calls the Docker service by default.**

When you want to debug locally, you need to **manually change where it routes** by creating an environment-specific config file.

---

## How It Works Now

### Docker Mode (Default)
```
API Gateway → http://auth-svc:80 (Docker service name)
```

The API Gateway looks for a service named `auth-svc` on the Docker network and calls it on port 80.

### Local Debug Mode (Manual)
You need to **tell the API Gateway** to route to your local machine instead of Docker.

---

## Two Ways to Debug Locally

### Option 1: Change Port Mapping (Simple - Recommended)

**Don't change any config files.** Just map the Docker port to your local port:

```yaml
# docker-compose.yml - auth service section
ports:
  - "5011:80"  # External port 5011 → Container port 80
```

Now when you:
1. Stop Docker auth service: `docker compose stop auth-svc`
2. Run locally on port 5011
3. API Gateway still calls `auth-svc:80` but since Docker is stopped, **it fails**
4. **This doesn't work automatically!**

### Option 2: Use Environment Variable (Best - Auto-Switch)

Create a simple environment variable that changes based on where the service runs.

**In docker-compose.yml:**
```yaml
api-gateway:
  environment:
    - AUTH_SERVICE_URL=${AUTH_SERVICE_URL:-http://auth-svc:80}
```

**To use Docker (default):**
```bash
# Don't set anything - uses default
docker compose up -d
```

**To use local debug:**
```bash
# Set environment variable before starting
$env:AUTH_SERVICE_URL="http://host.docker.internal:5011"
docker compose restart api-gateway
```

---

## Current Setup (What You Have)

**Base config (appsettings.json):**
```json
"auth-cluster": {
  "Address": "http://auth-svc:80"
}
```

**Development override (appsettings.Development.json):**
```json
"auth-cluster": {
  "Address": "http://auth-svc:80"  // Same as base
}
```

**Result:** API Gateway ALWAYS calls Docker `auth-svc:80`

---

## The Problem with Auto-Detection

YARP (the reverse proxy) **cannot automatically detect** which service is available. You must tell it explicitly.

**Why?**
- `auth-svc:80` is a Docker network name - only works inside Docker
- `host.docker.internal:5011` points to your Windows machine
- These are **two completely different addresses**
- The gateway must be configured to use ONE of them

---

## Recommended Solution: Simple Script

Create two scripts to switch modes:

###  debug-local.cmd
```cmd
@echo off
echo Switching to LOCAL debug mode...
docker compose stop auth-svc
echo Auth service stopped. Run it from your IDE on port 5011.
```

### debug-docker.cmd
```cmd
@echo off
echo Switching to DOCKER mode...
docker compose start auth-svc
echo Auth service started in Docker.
```

---

## Why We Can't Auto-Switch

You might think: "Why not configure both addresses and let it try both?"

**The problem:**
1. YARP tries the first address
2. If it's down, it returns an error immediately
3. It does NOT automatically try the second address
4. You'd need health checks + complex failover logic

**It's simpler to just:**
1. Stop Docker service when debugging
2. Start Docker service when done
3. API Gateway always points to `auth-svc:80`
4. When Docker is stopped, you manually run on a different port for local testing

---

## Current Behavior

**When Docker auth-svc is running:**
- ✅ API Gateway → `auth-svc:80` → Docker container
- ✅ Login works

**When Docker auth-svc is stopped and you run locally on 5011:**
- ❌ API Gateway → `auth-svc:80` → NOT FOUND (502 error)
- ❌ Login fails
- Your local service on 5011 is running but nothing calls it

---

## The Fix You Need

To make local debugging work, you have **3 choices**:

### Choice 1: Run on Port 80 Locally (Complex)
Make your local service listen on port 80 (requires admin rights on Windows)

### Choice 2: Change Docker Compose (Breaking)
Make Docker auth service use a different internal name when you're debugging

### Choice 3: Manually Update Config (Current - Simple)

**When debugging:**
1. Edit `appsettings.Development.json`:
   ```json
   "Address": "http://host.docker.internal:5011"
   ```
2. Restart API Gateway: `docker compose restart api-gateway`
3. Stop Docker auth: `docker compose stop auth-svc`
4. Run locally from IDE

**When done:**
1. Edit `appsettings.Development.json`:
   ```json
   "Address": "http://auth-svc:80"
   ```
2. Restart API Gateway: `docker compose restart api-gateway`
3. Start Docker auth: `docker compose start auth-svc`

---

## Summary

**Q: How does API Gateway know which endpoint to call?**

**A: It doesn't "know" - you must tell it explicitly in the config file.**

Current config says: "Always call `http://auth-svc:80`"

To debug locally:
1. Change config to point to `http://host.docker.internal:5011`
2. Restart API Gateway
3. Stop Docker auth service
4. Run from IDE

**There is no automatic switching** - you control it manually.

---

## Quick Reference

**Default (Docker):**
```
Address: "http://auth-svc:80"
```

**Local Debug:**
```
Address: "http://host.docker.internal:5011"
```

**Where:** `C:\Gearify\gearify-api-gateway\appsettings.Development.json`

**Restart:** `docker compose restart api-gateway`
