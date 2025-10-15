# Gearify Health Check Guide

This guide shows you how to check the health status of all Gearify services locally.

## Quick Commands

### 1. Check All Containers Status
```bash
cd C:\Gearify\gearify-umbrella
docker compose ps
```
Shows all containers with their status and ports.

### 2. Check Only Running Containers
```bash
docker compose ps --status running
```

### 3. Check Specific Service
```bash
docker compose ps <service-name>
# Example:
docker compose ps cart-svc
```

## Health Check Methods

### Method 1: Docker Compose Status
```bash
cd C:\Gearify\gearify-umbrella
docker compose ps --format "table {{.Service}}\t{{.Status}}\t{{.Ports}}"
```

### Method 2: Container Health Status
For containers with health checks (postgres, redis, localstack):
```bash
docker inspect gearify-localstack --format='{{.State.Health.Status}}'
docker inspect gearify-postgres --format='{{.State.Health.Status}}'
docker inspect gearify-redis --format='{{.State.Health.Status}}'
```

### Method 3: View Container Logs
```bash
# Last 20 lines
docker logs gearify-api-gateway --tail 20

# Follow logs in real-time
docker logs -f gearify-cart-svc

# View logs with timestamps
docker logs -t gearify-tenant-svc --tail 50
```

### Method 4: Test Service Endpoints
```bash
# Test API Gateway
curl http://localhost:8080/health

# Test individual microservices
curl http://localhost:5001/health  # Catalog
curl http://localhost:5002/health  # Search
curl http://localhost:5003/health  # Cart
curl http://localhost:5004/health  # Order
curl http://localhost:5005/health  # Payment
curl http://localhost:5006/health  # Shipping
curl http://localhost:5007/health  # Inventory
curl http://localhost:5008/health  # Tenant
curl http://localhost:5009/health  # Media
curl http://localhost:5010/health  # Notification
```

### Method 5: Use the Health Check Script
```bash
cd C:\Gearify\gearify-umbrella
bash scripts/check-services.sh
```

### Method 6: PowerShell Health Check
```powershell
cd C:\Gearify\gearify-umbrella
powershell -ExecutionPolicy Bypass -File scripts/health-check.ps1
```

## Port Accessibility Check

```bash
# Check if ports are listening
netstat -an | findstr "8080 5001 5002 5003 5004 5005 5006 5007 5008 5009 5010 4200"
```

## Service URLs

### Application Services
- **Web App**: http://localhost:4200
- **API Gateway**: http://localhost:8080
- **Tenant Service**: http://localhost:5008
- **Catalog Service**: http://localhost:5001
- **Search Service**: http://localhost:5002
- **Cart Service**: http://localhost:5003
- **Order Service**: http://localhost:5004
- **Payment Service**: http://localhost:5005
- **Shipping Service**: http://localhost:5006
- **Inventory Service**: http://localhost:5007
- **Media Service**: http://localhost:5009
- **Notification Service**: http://localhost:5010

### Infrastructure Services
- **LocalStack**: http://localhost:4566
- **PostgreSQL**: localhost:5432
- **Redis**: localhost:6379

### Observability Services
- **Seq (Logs)**: http://localhost:5341
- **Jaeger (Tracing)**: http://localhost:16686
- **Prometheus (Metrics)**: http://localhost:9090
- **Grafana (Dashboards)**: http://localhost:3000
- **MailHog (Email)**: http://localhost:8025

## Troubleshooting

### Check Why a Container is Down
```bash
docker compose ps -a <service-name>
docker logs <container-name> --tail 50
```

### Restart a Specific Service
```bash
docker compose restart <service-name>
# Example:
docker compose restart cart-svc
```

### Rebuild and Restart
```bash
docker compose up -d --build <service-name>
```

### View Resource Usage
```bash
docker stats --no-stream
```

### Check Container Network
```bash
docker network inspect gearify-network
```

## Expected Healthy Status

All services should show:
- **STATUS**: `Up X minutes/hours` (not `Exited`)
- **Infrastructure** (postgres, redis, localstack): Should show `(healthy)` after status
- **Ports**: Should map correctly (e.g., `0.0.0.0:8080->80/tcp`)

### Healthy Example
```
gearify-cart-svc    Up 38 minutes    5003/tcp, 0.0.0.0:5003->80/tcp
gearify-postgres    Up 4 hours (healthy)    0.0.0.0:5432->5432/tcp
```

### Unhealthy Example
```
gearify-cart-svc    Exited (1) 2 minutes ago
gearify-postgres    Up 4 hours (unhealthy)    0.0.0.0:5432->5432/tcp
```

## Quick Health Summary

Run this one-liner to get a quick overview:
```bash
cd C:\Gearify\gearify-umbrella && docker compose ps --format "table {{.Service}}\t{{.Status}}" | grep -E "Service|svc|gateway|web"
```

## Monitoring in Real-Time

### Watch container status (updates every 2 seconds)
```bash
watch -n 2 'docker compose ps'
```

### Monitor logs from multiple services
```bash
docker compose logs -f api-gateway cart-svc tenant-svc
```

## Container Resource Usage

```bash
# View CPU, Memory usage
docker stats

# View only specific containers
docker stats gearify-api-gateway gearify-cart-svc gearify-postgres
```
