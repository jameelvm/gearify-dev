# Gearify Quick Start Guide

## Prerequisites

- **Docker Desktop** (4.20+) with at least 16GB RAM allocated
- **Node.js 18+**
- **.NET 8 SDK**
- **Git**
- **AWS CLI** installed with `awslocal` wrapper
- **LocalStack Pro License** (get from https://app.localstack.cloud)

### Install AWS CLI + awslocal (if not installed)

**Windows:**
```powershell
# Install AWS CLI
msiexec.exe /i https://awscli.amazonaws.com/AWSCLIV2.msi

# Install awslocal wrapper
pip install awscli-local
```

**macOS/Linux:**
```bash
# Install AWS CLI
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install

# Install awslocal
pip install awscli-local
```

---

## Step 1: Get LocalStack Pro License

1. Go to https://app.localstack.cloud
2. Sign up or login
3. Navigate to **API Keys**
4. Copy your API key

---

## Step 2: Setup Environment

### Windows (PowerShell)

```powershell
# Navigate to gearify-umbrella
cd C:\Gearify\gearify-umbrella

# Copy environment template
Copy-Item .env.template .env

# Open .env and add your LocalStack API key
notepad .env
```

### macOS/Linux (Bash)

```bash
# Navigate to gearify-umbrella
cd gearify-umbrella

# Copy environment template
cp .env.template .env

# Edit .env and add your LocalStack API key
nano .env  # or vim, code, etc.
```

**Add this line to .env:**
```bash
LOCALSTACK_API_KEY=your-actual-api-key-from-step-1
```

---

## Step 3: Clone Service Repositories (Optional)

If you want to build services locally:

### Windows
```powershell
.\scripts\clone-all.ps1
```

### macOS/Linux
```bash
./scripts/clone-all.sh
```

**Note**: This step is optional. You can skip it and just run infrastructure services for now.

---

## Step 4: Start All Services

### Windows
```powershell
# Using make.bat
make.bat up

# Or using Docker Compose directly
docker compose up -d
```

### macOS/Linux
```bash
make up
```

**Wait 2-3 minutes** for all services to initialize. You'll see:
- LocalStack Pro starting
- DynamoDB, S3, SQS, SNS, Cognito being created
- PostgreSQL, Redis starting
- Observability stack (Seq, Jaeger, Grafana) starting

---

## Step 5: Verify Services Started

```powershell
# Check running containers
docker ps

# Check LocalStack status
docker exec gearify-localstack localstack status services

# Or use make target
make localstack-status
```

**Expected Output:**
```
┌────────────────┬──────────┐
│ Service        │ Status   │
├────────────────┼──────────┤
│ cognito-idp    │ running  │
│ dynamodb       │ running  │
│ s3             │ running  │
│ sqs            │ running  │
│ sns            │ running  │
│ secretsmanager │ running  │
│ ssm            │ running  │
└────────────────┴──────────┘
```

---

## Step 6: Verify AWS Resources Created

```powershell
make aws-resources
```

**Should display:**
- ✅ DynamoDB Tables: gearify-products, gearify-orders, gearify-tenants, gearify-feature-flags
- ✅ S3 Buckets: gearify-product-images, gearify-assets
- ✅ SQS Queues: gearify-order-events, gearify-payment-events, gearify-notification-events
- ✅ SNS Topics: gearify-order-topic, gearify-payment-topic
- ✅ Cognito User Pool: gearify-users

---

## Step 7: Seed Test Data

### Windows
```powershell
.\scripts\seed.ps1
```

### macOS/Linux
```bash
./scripts/seed.sh
```

**This will seed:**
- 2 Tenants: `default` and `global-demo`
- 20+ Cricket Products (bats, pads, gloves, balls, helmets)
- Feature Flags (enable-checkout, enable-stripe, enable-paypal)
- Sample payment transactions in PostgreSQL

**Wait for completion message:**
```
✅ Seeding complete!
```

---

## Step 8: Test Cognito Authentication

```powershell
make cognito-login
```

**Expected output:**
```json
{
  "AuthenticationResult": {
    "AccessToken": "eyJra...",
    "IdToken": "eyJra...",
    "RefreshToken": "eyJra...",
    "ExpiresIn": 3600,
    "TokenType": "Bearer"
  }
}
```

**Demo Credentials:**
- **admin@gearify.com** / Admin123!
- **user@global-demo.com** / User123!

---

## Step 9: Access Services in Browser

Open these URLs:

| Service | URL | Credentials |
|---------|-----|-------------|
| **Seq (Logs)** | http://localhost:5341 | - |
| **Jaeger (Traces)** | http://localhost:16686 | - |
| **Grafana (Metrics)** | http://localhost:3000 | admin / admin |
| **Prometheus** | http://localhost:9090 | - |
| **MailHog (Emails)** | http://localhost:8025 | - |

---

## Step 10: Verify Seeded Data

### Check DynamoDB Products
```powershell
aws --endpoint-url=http://localhost:4566 dynamodb scan `
  --table-name gearify-products `
  --limit 5
```

### Check Tenants
```powershell
aws --endpoint-url=http://localhost:4566 dynamodb scan `
  --table-name gearify-tenants
```

### Check PostgreSQL Payments
```powershell
docker exec -it gearify-postgres psql -U postgres -d gearify -c "SELECT * FROM payment_transactions;"
```

### Check Redis
```powershell
docker exec gearify-redis redis-cli KEYS "*"
```

---

## Step 11: View Logs

### All Services
```powershell
make logs
```

### Specific Service
```powershell
docker logs -f gearify-localstack
docker logs -f gearify-postgres
docker logs -f gearify-redis
```

### Seq (Structured Logs)
Go to http://localhost:5341 and explore structured logs

---

## Quick Verification Checklist

Run these commands to verify everything is working:

```powershell
# ✅ Check all containers running
docker ps | Select-String "gearify"

# ✅ Check LocalStack health
curl http://localhost:4566/_localstack/health

# ✅ List DynamoDB tables
aws --endpoint-url=http://localhost:4566 dynamodb list-tables

# ✅ Verify products exist
aws --endpoint-url=http://localhost:4566 dynamodb scan --table-name gearify-products --limit 1

# ✅ Check Cognito users
aws --endpoint-url=http://localhost:4566 cognito-idp list-users --user-pool-id <pool-id-from-logs>

# ✅ View service status
make ps
```

---

## Common Make Commands

```bash
make help              # Show all available commands
make up                # Start all services
make down              # Stop all services
make restart           # Restart services
make logs              # View logs
make ps                # Show container status
make seed              # Seed databases
make seed-clean        # Wipe and re-seed data
make validate-env      # Check environment variables
make localstack-status # Check LocalStack services
make aws-resources     # List AWS resources
make cognito-login     # Test Cognito auth
make clean             # Remove everything
```

---

## Running Tests (Optional)

### End-to-End Tests (Playwright)
```powershell
cd tests/e2e
npm install
npx playwright install
npx playwright test

# Or use make
make test-e2e
```

### Load Tests (k6)
```powershell
cd tests/load
k6 run checkout-flow.js

# Or use make
make test-load
```

---

## Troubleshooting

### Issue: License Key Not Working

**Solution:**
```powershell
# Check if key is set
docker exec gearify-localstack env | grep LOCALSTACK_API_KEY

# Restart LocalStack
docker restart gearify-localstack

# Check logs
docker logs gearify-localstack
```

### Issue: Services Won't Start

**Solution:**
```powershell
# Check Docker is running
docker ps

# Check logs
docker logs gearify-localstack
docker logs gearify-postgres
docker logs gearify-redis

# Verify .env file
Get-Content .env | Select-String "LOCALSTACK_API_KEY"
```

### Issue: Resources Not Created

**Solution:**
```powershell
# Manually run init script
docker exec gearify-localstack bash /etc/localstack/init/ready.d/init-aws.sh

# Check output for errors
docker logs gearify-localstack | Select-String "ERROR"
```

### Issue: Seed Script Fails

**Solution:**
```powershell
# Ensure services are healthy
docker ps --filter health=healthy

# Wait for LocalStack to be ready
Start-Sleep -Seconds 30

# Try seeding again
.\scripts\seed.ps1
```

### Issue: Port Conflicts

**Solution:**
```powershell
# Check what's using ports
netstat -ano | findstr ":4566"
netstat -ano | findstr ":5432"
netstat -ano | findstr ":6379"

# Stop conflicting services or change ports in docker-compose.yml
```

---

## Stopping Services

### Stop All
```powershell
make down
```

### Stop and Remove Volumes
```powershell
make clean
```

---

## Clean Restart

If you need to start completely fresh:

```powershell
# Remove everything
make clean

# Start services
make up

# Wait 2-3 minutes

# Seed data
make seed
```

---

## Next Steps

### 1. Build Application Services

If you cloned service repos, build them:
```powershell
make build-all
```

### 2. Start Application Services

Update `docker-compose.yml` to uncomment app services, then:
```powershell
make restart
```

### 3. Access Frontend

If frontend is running:
- http://localhost:4200

### 4. Explore Documentation

- **Architecture**: `docs/ARCHITECTURE.md`
- **Operations**: `docs/RUNBOOK.md`
- **Debugging**: `docs/DEBUGGING.md`
- **LocalStack Pro**: `docs/LOCALSTACK-PRO.md`
- **Troubleshooting**: `docs/TROUBLESHOOTING.md`
- **Customization**: `docs/CUSTOMIZE.md`
- **Data Models**: `docs/DATA-MODELS.md`
- **Contributing**: `docs/CONTRIBUTING.md`

---

## Getting Help

- Check logs: `make logs`
- View service status: `make ps`
- LocalStack status: `make localstack-status`
- AWS resources: `make aws-resources`
- Read troubleshooting: `docs/TROUBLESHOOTING.md`

---

## Success Indicators

You'll know everything is working when:

✅ All Docker containers are running and healthy
✅ LocalStack services show as "running"
✅ DynamoDB tables contain seeded data
✅ Cognito authentication returns JWT tokens
✅ Seq shows logs at http://localhost:5341
✅ Jaeger shows traces at http://localhost:16686
✅ No errors in container logs

---

**You're now ready to develop locally with the full Gearify stack!**
