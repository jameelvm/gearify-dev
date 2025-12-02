# Development Mode Setup - Hot Reload Guide

This guide explains how to set up and use development mode for the Gearify frontend with instant hot reload.

---

## Table of Contents
1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [How It Works](#how-it-works)
4. [Production vs Development Mode](#production-vs-development-mode)
5. [Switching to Dev Mode](#switching-to-dev-mode)
6. [Switching Back to Production Mode](#switching-back-to-production-mode)
7. [Troubleshooting](#troubleshooting)
8. [FAQ](#faq)

---

## Overview

**Development Mode Features:**
- ✅ **Hot Reload** - Changes appear instantly in the browser
- ✅ **No Rebuild** - Save file and see changes immediately
- ✅ **Fast Iteration** - Typical Angular dev experience
- ✅ **Source Maps** - Easy debugging in browser DevTools
- ✅ **Better Logging** - Detailed error messages

**Production Mode:**
- ✅ **Optimized Build** - Minified, tree-shaken, production-ready
- ✅ **Fast Loading** - Served by nginx, very performant
- ❌ **Slow Changes** - 3-5 minute rebuild for every change
- ❌ **No Hot Reload** - Must rebuild container

---

## Quick Start

### Start Development Mode (Hot Reload)

```bash
# 1. Stop current production container
docker stop gearify-web
docker rm gearify-web

# 2. Start dev mode
cd C:\Gearify
docker-compose -f docker-compose.dev.yml up -d

# 3. View logs (optional)
docker logs -f gearify-web
```

**That's it!** Now when you edit any `.ts`, `.html`, or `.scss` file in `gearify-web/src`, the browser will automatically reload.

### Test Hot Reload

1. Open `gearify-web/src/app/shared/components/navbar/navbar.component.ts`
2. Change line 103: `brand-name` text
3. Save the file
4. Watch your browser at `http://default.localhost.direct:4200` - it refreshes automatically!

---

## How It Works

This section explains the Docker configuration files and how dev mode achieves hot reload.

### Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│  Your Computer (Host Machine)                       │
│                                                      │
│  C:\Gearify\gearify-web\src\                       │
│  ├── app\                                           │
│  │   ├── core\                                      │
│  │   │   └── services\                              │
│  │   │       └── auth.service.ts  ← You edit this  │
│  │   └── ...                                        │
│  └── ...                                            │
│                                                      │
│         ↓ ↑  (Volume Mount - Live Sync)             │
│                                                      │
│  ┌────────────────────────────────────────────┐     │
│  │  Docker Container: gearify-web             │     │
│  │                                             │     │
│  │  /app/src/                                  │     │
│  │  ├── app/                                   │     │
│  │  │   ├── core/                              │     │
│  │  │   │   └── services/                      │     │
│  │  │   │       └── auth.service.ts ← Synced!  │     │
│  │  │   └── ...                                │     │
│  │  └── ...                                    │     │
│  │                                             │     │
│  │  Angular Dev Server (npm start)            │     │
│  │  - Watches for file changes                │     │
│  │  - Recompiles TypeScript                   │     │
│  │  - Notifies browser to reload              │     │
│  │                                             │     │
│  │  Port 4200 → Host Port 4200                │     │
│  └────────────────────────────────────────────┘     │
│                                                      │
│         ↓ ↑  (HTTP)                                 │
│                                                      │
│  Your Browser: http://localhost:4200               │
│  - Receives reload signal                           │
│  - Auto-refreshes with new code                     │
└─────────────────────────────────────────────────────┘
```

### File: `Dockerfile.dev`

**Location:** `gearify-web/Dockerfile.dev`

This file defines how to build the development Docker image.

```dockerfile
FROM node:20-alpine
```
**What it does:** Uses Node.js 20 on Alpine Linux (lightweight)
**Why:** Angular requires Node.js to run `npm` and the dev server

```dockerfile
WORKDIR /app
```
**What it does:** Sets the working directory inside container to `/app`
**Why:** All subsequent commands run from this directory

```dockerfile
COPY package*.json ./
```
**What it does:** Copies `package.json` and `package-lock.json` to `/app/`
**Why:** Needed before `npm install` to download dependencies

```dockerfile
RUN npm install
```
**What it does:** Downloads and installs all npm packages (node_modules)
**Why:** Installs Angular CLI, TypeScript compiler, and other dependencies
**Time:** Takes 2-3 minutes on first build (cached afterward)

```dockerfile
COPY . .
```
**What it does:** Copies entire `gearify-web/` directory to `/app/`
**Why:** Provides the source code for the Angular app
**Note:** This gets overridden by volume mount when container starts

```dockerfile
EXPOSE 4200
```
**What it does:** Documents that the container will listen on port 4200
**Why:** Angular dev server runs on port 4200 by default
**Note:** This is just documentation; actual port mapping is in docker-compose.yml

```dockerfile
CMD ["npm", "start"]
```
**What it does:** Runs `npm start` when container starts
**Why:** Starts the Angular development server
**What `npm start` does:** Runs `ng serve --host 0.0.0.0 --port 4200 --disable-host-check --poll=2000`

---

### File: `docker-compose.dev.yml`

**Location:** `docker-compose.dev.yml` (in root directory)

This file orchestrates the Docker container with the right configuration.

```yaml
version: '3.9'
```
**What it does:** Specifies Docker Compose file format version
**Why:** Ensures compatibility with Docker Compose features
**Note:** Warning says it's obsolete, but it's still supported

```yaml
services:
  gearify-web:
```
**What it does:** Defines a service named `gearify-web`
**Why:** Docker Compose can manage multiple services; this names our web app

```yaml
    build:
      context: ./gearify-web
      dockerfile: Dockerfile.dev
```
**What it does:**
- `context`: Sets build context to `gearify-web/` directory
- `dockerfile`: Specifies which Dockerfile to use

**Why:**
- Context determines where `COPY . .` copies from
- Using `Dockerfile.dev` instead of `Dockerfile` (production)

**What happens:** When you run `docker-compose build`, it:
1. Changes to `gearify-web/` directory
2. Reads `Dockerfile.dev`
3. Builds the image with source code from `gearify-web/`

```yaml
    container_name: gearify-web
```
**What it does:** Names the container `gearify-web`
**Why:** Easy to reference with `docker logs gearify-web`, `docker exec gearify-web`, etc.

```yaml
    ports:
      - "4200:4200"
```
**What it does:** Maps host port 4200 to container port 4200
**Why:** Allows you to access `http://localhost:4200` on your computer

**Breakdown:**
- `4200` (left): Port on your computer
- `4200` (right): Port inside container where Angular dev server runs

```yaml
    volumes:
      - ./gearify-web:/app
      - /app/node_modules
```
**What it does:** Mounts directories from host to container

**First volume: `./gearify-web:/app`**
- **Left side (`./gearify-web`)**: Directory on your computer
- **Right side (`/app`)**: Directory inside container
- **Effect**: Files in `C:\Gearify\gearify-web` appear as `/app` in container
- **Result**: When you edit a file, it's instantly visible inside container

**Second volume: `/app/node_modules`**
- **What it does:** Creates an anonymous volume for `node_modules`
- **Why:** Prevents your host's `node_modules` (if any) from overwriting container's
- **Result:** Container keeps its own `node_modules` (300MB of dependencies)

**Visual:**
```
Host: C:\Gearify\gearify-web\src\app\core\services\auth.service.ts
              ↓ ↑ (synced via volume)
Container: /app/src/app/core/services/auth.service.ts
```

```yaml
    environment:
      - NODE_ENV=development
      - CHOKIDAR_USEPOLLING=true
```
**What it does:** Sets environment variables inside container

**NODE_ENV=development:**
- Tells Node.js this is development mode
- Angular uses this for dev-specific features

**CHOKIDAR_USEPOLLING=true:**
- **Critical for Docker file watching!**
- **What it does:** Makes file watcher check for changes every 2 seconds
- **Why needed:** Docker on Windows/Mac doesn't support native file system events
- **Without this:** File changes won't be detected, no hot reload

```yaml
    networks:
      - gearify-network
```
**What it does:** Connects container to `gearify-network`
**Why:** Allows communication with other containers (auth service, database, etc.)

```yaml
    restart: unless-stopped
```
**What it does:** Automatically restarts container if it crashes
**Why:** Keeps dev server running even after errors

```yaml
    stdin_open: true
    tty: true
```
**What it does:**
- `stdin_open`: Keeps STDIN open (like `-i` in `docker run`)
- `tty`: Allocates a pseudo-TTY (like `-t` in `docker run`)

**Why:** Allows interactive terminal features (e.g., Angular dev server prompts)

```yaml
networks:
  gearify-network:
    external: true
```
**What it does:** References an existing Docker network
**Why:** Network already created by main docker-compose.yml

---

### How Hot Reload Works: Step by Step

**1. You save a file** (`auth.service.ts`)
```
Your Editor → Saves to C:\Gearify\gearify-web\src\app\core\services\auth.service.ts
```

**2. Docker volume syncs it to container**
```
Volume Mount → File appears at /app/src/app/core/services/auth.service.ts
```

**3. Chokidar (file watcher) detects the change**
```
Chokidar (polling every 2 sec) → "auth.service.ts changed!"
```

**4. Angular dev server recompiles**
```
ng serve → Runs TypeScript compiler
TypeScript → Compiles .ts to .js
Webpack → Bundles the JavaScript
```

**5. Dev server notifies browser**
```
WebSocket connection → Sends "reload" message to browser
```

**6. Browser auto-refreshes**
```
Browser → Reloads page with new code
You → See changes instantly!
```

**Total time:** 2-5 seconds from save to browser refresh!

---

### Package.json Start Script Explained

**File:** `gearify-web/package.json`

```json
"start": "ng serve --host 0.0.0.0 --port 4200 --disable-host-check --poll=2000"
```

**Breakdown of each flag:**

**`ng serve`**
- Angular CLI command to start development server
- Watches files, compiles TypeScript, serves app

**`--host 0.0.0.0`**
- Listen on all network interfaces (not just localhost)
- **Without this:** Can only access from inside container
- **With this:** Can access from host machine at localhost:4200

**`--port 4200`**
- Run dev server on port 4200
- Standard Angular port

**`--disable-host-check`**
- Disables Host header check
- **Why needed:** Docker routing can cause mismatched Host headers
- **Without this:** "Invalid Host header" errors

**`--poll=2000`**
- **CRITICAL for Docker!**
- Poll filesystem every 2000ms (2 seconds) for changes
- **Why needed:** Docker volume mounts don't support native file system events
- **Without this:** Changes won't be detected, no hot reload
- **Alternative values:** 1000 (faster but more CPU), 5000 (slower but less CPU)

---

### Why Volume Mounts Are Fast

**Question:** Why doesn't mounting slow down the app?

**Answer:** Volume mounts are nearly instant because:

1. **No copying:** Files aren't copied; it's like a symbolic link
2. **Direct access:** Container reads files directly from host filesystem
3. **Kernel-level:** Handled by Docker/Linux kernel, very efficient

**Benchmark:**
- Reading a file from volume: ~0.1ms
- Same as reading from container's own filesystem

**Only caveat:** node_modules
- node_modules has 100,000+ small files
- Reading these through volume mount would be slow
- That's why we use `/app/node_modules` anonymous volume
- Keeps node_modules in container's filesystem (fast)

---

## Production vs Development Mode

### Production Mode (Current Setup)

**Dockerfile:** `gearify-web/Dockerfile`

**How it works:**
1. Builds Angular app with `ng build` (production optimized)
2. Copies built files to nginx
3. Serves static files from nginx

**When to use:**
- Final testing before deployment
- Performance testing
- Production deployment

**Workflow for changes:**
```bash
# Edit code
# Rebuild (takes 3-5 minutes)
docker build -t gearify-umbrella-web -f gearify-web/Dockerfile .

# Restart container
docker stop gearify-web
docker rm gearify-web
docker run -d --name gearify-web -p 4200:80 --network gearify-network gearify-umbrella-web
```

---

### Development Mode (Recommended for Development)

**Dockerfile:** `gearify-web/Dockerfile.dev`
**Docker Compose:** `docker-compose.dev.yml`

**How it works:**
1. Runs `npm start` (Angular dev server)
2. Mounts source code as volume
3. Watches for file changes
4. Auto-reloads browser

**When to use:**
- Active development (what you're doing now!)
- Bug fixing
- Feature implementation
- Rapid iteration

**Workflow for changes:**
```bash
# Edit code
# Save file
# Browser auto-refreshes - DONE!
```

---

## Switching to Dev Mode

### Step-by-Step Instructions

#### 1. Stop Current Container

```bash
docker stop gearify-web
docker rm gearify-web
```

#### 2. Build Dev Image (First Time Only)

```bash
cd C:\Gearify
docker-compose -f docker-compose.dev.yml build
```

**Note:** This takes 2-3 minutes the first time to install npm dependencies. After that, starting is instant!

#### 3. Start Dev Mode

```bash
docker-compose -f docker-compose.dev.yml up -d
```

#### 4. Verify It's Working

```bash
# Check container status
docker ps | grep gearify-web

# Should show:
# - Port 4200:4200 (not 4200:80)
# - Status: Up

# View logs
docker logs -f gearify-web

# You should see:
# ✔ Browser application bundle generation complete.
# ✔ Built at: [timestamp]
# ** Angular Live Development Server is listening on 0.0.0.0:4200 **
```

#### 5. Test Hot Reload

Open browser to: `http://default.localhost.direct:4200`

**Make a test change:**

Edit `gearify-web/src/app/shared/components/navbar/navbar.component.ts`:

```typescript
// Line 103 - Change the brand name
.brand-name {
  font-size: 1.5rem;
  font-weight: 700;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  // Add this for testing:
  text-decoration: underline; // ← ADD THIS LINE
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}
```

**Save the file** and watch your browser - the change appears instantly!

---

## Switching Back to Production Mode

When you want to test production build:

```bash
# 1. Stop dev mode
docker-compose -f docker-compose.dev.yml down

# 2. Build production image
cd C:\Gearify
docker build -t gearify-umbrella-web -f gearify-web/Dockerfile .

# 3. Start production container
docker run -d \
  --name gearify-web \
  -p 4200:80 \
  --network gearify-network \
  gearify-umbrella-web
```

---

## Troubleshooting

### Problem: Changes Not Appearing

**Solution 1: Check if dev mode is actually running**

```bash
docker ps | grep gearify-web
```

Should show `4200:4200` (not `4200:80`)

**Solution 2: Check logs for compilation errors**

```bash
docker logs -f gearify-web
```

Look for:
- ✅ `Built at: [timestamp]` - means compilation succeeded
- ❌ `ERROR in...` - means compilation failed

**Solution 3: Hard refresh browser**

- Windows: `Ctrl + Shift + R`
- Mac: `Cmd + Shift + R`

**Solution 4: Restart container**

```bash
docker-compose -f docker-compose.dev.yml restart
```

---

### Problem: Port Already in Use

**Error:** `port is already allocated`

**Solution:**

```bash
# Find what's using port 4200
docker ps | grep 4200

# Stop the old container
docker stop gearify-web
docker rm gearify-web

# Start dev mode
docker-compose -f docker-compose.dev.yml up -d
```

---

### Problem: File Changes Not Detected

**Cause:** File watching doesn't work well in Docker on Windows/Mac

**Solution:** Already fixed! The `docker-compose.dev.yml` includes:

```yaml
environment:
  - CHOKIDAR_USEPOLLING=true  # Enables polling for file changes
```

And `package.json` has:

```json
"start": "ng serve --host 0.0.0.0 --port 4200 --disable-host-check"
```

If still not working, add `--poll` flag:

Edit `gearify-web/package.json`:

```json
"start": "ng serve --host 0.0.0.0 --port 4200 --disable-host-check --poll=2000"
```

Then restart:

```bash
docker-compose -f docker-compose.dev.yml restart
```

---

### Problem: npm install Errors

**Error during build:** `npm ERR! code ENOENT`

**Solution:**

```bash
# Clear Docker build cache
docker builder prune -a

# Rebuild
docker-compose -f docker-compose.dev.yml build --no-cache
```

---

### Problem: Container Exits Immediately

**Check logs:**

```bash
docker logs gearify-web
```

**Common causes:**

1. **Missing node_modules:** Fixed by volume mount in compose file
2. **Port conflict:** Use different port in `docker-compose.dev.yml`
3. **Angular config error:** Check `angular.json` syntax

---

## FAQ

### Q: Can I run dev mode and production mode at the same time?

**A:** Yes, but they need different ports.

Edit `docker-compose.dev.yml`:

```yaml
ports:
  - "4201:4200"  # Dev mode on 4201
```

Then production runs on 4200, dev on 4201.

---

### Q: Will dev mode changes affect production container?

**A:** No. They're completely separate:
- Dev mode: Uses source code from `gearify-web/` folder
- Production: Has its own copy built into the image

---

### Q: How do I see compilation errors?

**A:** Watch the logs:

```bash
docker logs -f gearify-web
```

Errors appear in real-time as you save files.

---

### Q: Can I use my local node_modules instead of container's?

**A:** Not recommended. The volume mount excludes `node_modules`:

```yaml
volumes:
  - ./gearify-web:/app
  - /app/node_modules  # This keeps container's node_modules
```

This prevents version conflicts between host and container.

---

### Q: How do I update npm packages in dev mode?

**A:**

```bash
# Option 1: Rebuild the container
docker-compose -f docker-compose.dev.yml build --no-cache

# Option 2: Install inside running container
docker exec -it gearify-web npm install <package-name>

# Then commit the changes to package.json
```

---

### Q: What's the difference between `npm start` in dev mode vs locally?

**A:**

| Feature | Local | Docker Dev Mode |
|---------|-------|-----------------|
| Hot Reload | ✅ Yes | ✅ Yes |
| Speed | Faster | Slightly slower |
| Consistency | Depends on local setup | Always same (Node 20) |
| Port | Usually 4200 | 4200 (configurable) |

---

## Commands Cheat Sheet

### Development Mode

```bash
# Start dev mode
docker-compose -f docker-compose.dev.yml up -d

# Stop dev mode
docker-compose -f docker-compose.dev.yml down

# Restart dev mode
docker-compose -f docker-compose.dev.yml restart

# View logs
docker logs -f gearify-web

# Rebuild dev image
docker-compose -f docker-compose.dev.yml build

# Rebuild without cache
docker-compose -f docker-compose.dev.yml build --no-cache
```

### Production Mode

```bash
# Build production image
docker build -t gearify-umbrella-web -f gearify-web/Dockerfile .

# Run production container
docker run -d --name gearify-web -p 4200:80 --network gearify-network gearify-umbrella-web

# Stop production container
docker stop gearify-web
docker rm gearify-web
```

### Useful Docker Commands

```bash
# Check running containers
docker ps

# Check all containers (including stopped)
docker ps -a

# View container logs
docker logs gearify-web

# Follow logs in real-time
docker logs -f gearify-web

# Execute command in running container
docker exec -it gearify-web sh

# Remove all stopped containers
docker container prune

# Remove unused images
docker image prune
```

---

## Files Overview

### Development Mode Files

- **`gearify-web/Dockerfile.dev`** - Dev mode Dockerfile
- **`docker-compose.dev.yml`** - Dev mode compose configuration
- **`gearify-web/package.json`** - npm scripts (already configured)

### Production Mode Files

- **`gearify-web/Dockerfile`** - Production Dockerfile
- **No compose file** - Run directly with `docker run`

---

## Best Practices

### For Active Development

1. **Use dev mode** - Always use hot reload during development
2. **Commit often** - Dev mode doesn't save state
3. **Check logs** - Watch for compilation errors in real-time
4. **Test production build** - Before deploying, test with production mode

### For Testing

1. **Use production mode** - Test the actual build that goes to production
2. **Test performance** - Production build is optimized, dev is not
3. **Test bundle size** - Check actual file sizes

### For Deployment

1. **Always use production mode** - Never deploy dev mode
2. **Test first** - Run production build locally first
3. **Check for errors** - Ensure build completes without errors

---

## Next Steps

After switching to dev mode:

1. ✅ Edit `auth.service.ts` - logout changes will appear instantly
2. ✅ Edit `navbar.component.ts` - menu changes appear instantly
3. ✅ Edit any `.html` or `.scss` file - instant refresh
4. ✅ Continue developing without waiting for rebuilds!

---

**Questions?** Check the troubleshooting section or container logs: `docker logs -f gearify-web`

**Happy coding with hot reload! 🚀**
