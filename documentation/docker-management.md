# Docker Container Management Guide

## Overview
This guide explains how to build, deploy, and manage individual Docker containers in the Gearify microservices architecture.

## Understanding Docker Desktop vs CLI

**Docker Desktop** provides a GUI for:
- Viewing running containers
- Checking logs
- Inspecting images and volumes
- Starting/stopping containers manually

**Docker CLI** (required for builds and deployments):
- Building images from code changes
- Deploying updated containers
- Managing compose configurations

**Important**: Docker Desktop does NOT have a "rebuild" button. You must use CLI commands to rebuild containers when code or configuration changes.

---

## Quick Reference

### When to Restart vs Rebuild

| Change Type | Command | Why |
|------------|---------|-----|
| No code/config changes | `docker-compose restart <service>` | Just restart the container |
| Configuration file changes (e.g., appsettings.json) | `docker-compose build <service>` + `docker-compose up -d <service>` | Config is baked into image |
| Code changes | `docker-compose build <service>` + `docker-compose up -d <service>` | Code is compiled into image |
| Dependency changes (packages) | `docker-compose build <service>` + `docker-compose up -d <service>` | Dependencies installed during build |

---

## Common Commands

### Build and Deploy a Single Service

**Two-Step Process (Recommended)**
```bash
# Navigate to umbrella directory
cd C:/Gearify/gearify-umbrella

# Step 1: Build the service
docker-compose build <service-name>

# Step 2: Recreate and start the container
docker-compose up -d <service-name>
```

**Single Command (Build + Deploy)**
```bash
cd C:/Gearify/gearify-umbrella
docker-compose up -d --build <service-name>
```

### Examples for Each Service

```bash
# API Gateway
docker-compose build api-gateway && docker-compose up -d api-gateway

# Auth Service
docker-compose build auth-svc && docker-compose up -d auth-svc

# Catalog Service
docker-compose build catalog-svc && docker-compose up -d catalog-svc

# Cart Service
docker-compose build cart-svc && docker-compose up -d cart-svc

# Order Service
docker-compose build order-svc && docker-compose up -d order-svc

# Payment Service
docker-compose build payment-svc && docker-compose up -d payment-svc

# Shipping Service
docker-compose build shipping-svc && docker-compose up -d shipping-svc

# Inventory Service
docker-compose build inventory-svc && docker-compose up -d inventory-svc

# Search Service
docker-compose build search-svc && docker-compose up -d search-svc

# Tenant Service
docker-compose build tenant-svc && docker-compose up -d tenant-svc

# Media Service
docker-compose build media-svc && docker-compose up -d media-svc

# Notification Service
docker-compose build notification-svc && docker-compose up -d notification-svc
```

---

## Typical Workflows

### Scenario 1: You Changed API Gateway Configuration

```bash
cd C:/Gearify/gearify-umbrella

# Edit the config file
# C:/Gearify/gearify-api-gateway/appsettings.json

# Rebuild and deploy
docker-compose build api-gateway
docker-compose up -d api-gateway

# Verify it's running
docker logs gearify-api-gateway --tail 20
```

### Scenario 2: You Updated Auth Service Code

```bash
cd C:/Gearify/gearify-umbrella

# Make your code changes in C:/Gearify/gearify-auth-svc/

# Rebuild and deploy
docker-compose build auth-svc
docker-compose up -d auth-svc

# Check if it started successfully
docker logs gearify-auth-svc --tail 20
```

### Scenario 3: You Need to Rebuild Multiple Services

```bash
cd C:/Gearify/gearify-umbrella

# Build each service separately to avoid errors
docker-compose build auth-svc
docker-compose build catalog-svc
docker-compose build api-gateway

# Deploy them
docker-compose up -d auth-svc catalog-svc api-gateway
```

### Scenario 4: Running a Service Locally (Outside Docker)

If you're running a service locally for debugging:

1. Stop the Docker container:
```bash
docker-compose stop auth-svc
```

2. Run locally:
```bash
cd C:/Gearify/gearify-auth-svc
dotnet run
```

3. Make sure API Gateway routes to the correct address:
   - For Docker services: `http://<service-name>:80`
   - For local services: `http://host.docker.internal:<port>`

---

## Troubleshooting

### Container Won't Start
```bash
# Check logs for errors
docker logs gearify-<service-name>

# Check if port is already in use
docker ps -a

# Force recreate
docker-compose up -d --force-recreate <service-name>
```

### Build Fails with "Project Not Found"
```bash
# Make sure you're in the umbrella directory
cd C:/Gearify/gearify-umbrella

# Check Dockerfile paths are correct
cat gearify-<service>/Dockerfile
```

### Changes Not Reflected After Rebuild
```bash
# Remove old container completely
docker-compose down <service-name>

# Rebuild with no cache
docker-compose build --no-cache <service-name>

# Deploy fresh container
docker-compose up -d <service-name>
```

### All Services Failing to Build
```bash
# DON'T run this - it tries to rebuild everything:
# docker-compose build

# Instead, build only what you need:
docker-compose build api-gateway
```

---

## Useful Commands

### View Logs
```bash
# Real-time logs
docker logs -f gearify-<service-name>

# Last 50 lines
docker logs gearify-<service-name> --tail 50

# Logs since timestamp
docker logs gearify-<service-name> --since 2024-01-01T00:00:00
```

### Check Container Status
```bash
# All running containers
docker ps

# All containers (including stopped)
docker ps -a

# Specific service status
docker-compose ps <service-name>
```

### Stop/Start Services
```bash
# Stop a service
docker-compose stop <service-name>

# Start a stopped service
docker-compose start <service-name>

# Restart a service (no rebuild)
docker-compose restart <service-name>
```

### Clean Up
```bash
# Remove stopped containers
docker-compose down <service-name>

# Remove ALL containers and networks
docker-compose down

# Remove images too
docker-compose down --rmi all

# Remove volumes (WARNING: deletes data)
docker-compose down -v
```

---

## Best Practices

1. **Always build specific services individually** - avoid `docker-compose build` without a service name
2. **Check logs after deployment** - use `docker logs` to verify the container started successfully
3. **Use `--tail` when viewing logs** - avoid overwhelming output with `--tail 20` or `--tail 50`
4. **Don't delete volumes unless necessary** - they contain your database data
5. **Keep a local copy of important configs** - before rebuilding, ensure your changes are saved

---

## Service Architecture

```
                    ┌─────────────────┐
                    │  API Gateway    │  (Port 8080)
                    │  (Port 80)      │
                    └────────┬────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
    ┌───────▼──────┐  ┌─────▼─────┐  ┌──────▼──────┐
    │  Auth        │  │  Catalog   │  │   Cart      │
    │  Service     │  │  Service   │  │   Service   │
    └──────────────┘  └────────────┘  └─────────────┘
         ...              ...               ...
```

All services connect through the API Gateway on port 8080.

---

## Configuration Files Location

```
C:/Gearify/
├── gearify-api-gateway/
│   └── appsettings.json          # Gateway routes config
├── gearify-auth-svc/
│   └── appsettings.json          # Auth service config
├── gearify-catalog-svc/
│   └── appsettings.json          # Catalog service config
└── gearify-umbrella/
    └── docker-compose.yml         # Container orchestration
```

When you change any `appsettings.json` file, you must rebuild that service's container.

---

## Summary

**Remember**:
- Docker Desktop = Viewing tool
- Docker CLI = Build and deployment tool
- Always rebuild after config/code changes
- Build only the service you changed (not all services)
- Check logs after deployment to verify success

**Most Common Command Pattern**:
```bash
cd C:/Gearify/gearify-umbrella
docker-compose build <service-name>
docker-compose up -d <service-name>
docker logs gearify-<service-name> --tail 20
```
