# Gearify Umbrella - Local Development Orchestration

Complete local development environment for Gearify microservices platform.

## Prerequisites

- Docker Desktop
- Node.js 18+
- .NET 8 SDK
- AWS CLI with `awslocal` wrapper
- LocalStack Pro license key

## Quick Start

### Windows (PowerShell)

```powershell
# Clone umbrella repo
git clone <umbrella-repo-url>
cd gearify-umbrella

# Copy environment template
cp .env.template .env

# Edit .env and add your LOCALSTACK_API_KEY

# Clone all service repos
.\scripts\clone-all.ps1

# Start all services
docker compose up -d

# Wait for services to initialize (2-3 minutes)

# Seed data
.\scripts\seed.ps1

# Access frontend
start http://localhost:4200
```

### macOS/Linux (Bash)

```bash
# Clone umbrella repo
git clone <umbrella-repo-url>
cd gearify-umbrella

# Copy environment template
cp .env.template .env

# Edit .env and add your LOCALSTACK_API_KEY

# Clone all service repos
./scripts/clone-all.sh

# Start all services
docker compose up -d

# Wait for services to initialize (2-3 minutes)

# Seed data
./scripts/seed.sh

# Access frontend
open http://localhost:4200
```

## Service URLs

| Service | URL |
|---------|-----|
| Frontend | http://localhost:4200 |
| API Gateway | http://localhost:8080 |
| Seq (Logs) | http://localhost:5341 |
| Jaeger (Traces) | http://localhost:16686 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| MailHog | http://localhost:8025 |

## Demo Credentials

- **admin@gearify.com** / Admin123!
- **user@global-demo.com** / User123!

## Common Commands

```bash
make up           # Start all services
make down         # Stop all services
make logs         # View logs
make seed         # Seed databases
make test-e2e     # Run E2E tests
make clean        # Remove all containers/volumes
```

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Runbook](docs/RUNBOOK.md)
- [Debugging](docs/DEBUGGING.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)

## Support

See [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) for common issues.
