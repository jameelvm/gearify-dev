# Gearify Deployment Guide

## Prerequisites
- Docker and Docker Compose
- .NET 8 SDK
- Node.js 20+
- AWS CLI (for cloud deployment)

## Local Development

### Step 1: Bootstrap
```powershell
cd C:\Gearify
.\bootstrap-gearify-fixed.ps1
.\bootstrap-enhancements-fixed.ps1
.\bootstrap-complete-fixed.ps1
```

### Step 2: Start Services
```powershell
cd gearify-umbrella
docker-compose up --build -d
```

### Step 3: Verify
- Web: http://localhost:4200
- Gateway: http://localhost:8080
- Catalog: http://localhost:5001/swagger

## Cloud Deployment (AWS)

### Provision Infrastructure
```bash
cd gearify-infra-templates/terraform
terraform init
terraform apply
```

### Build and Push Images
```bash
docker build -t catalog-svc ./gearify-catalog-svc
docker tag catalog-svc:latest <ecr-url>/catalog-svc:latest
docker push <ecr-url>/catalog-svc:latest
```

### Deploy with Helm
```bash
helm install catalog-service ./helm/catalog-service
```

## Monitoring

Start monitoring stack:
```bash
docker-compose -f docker-compose.monitoring.yml up -d
```

Access dashboards:
- Grafana: http://localhost:3000 (admin/admin)
- Prometheus: http://localhost:9090

## Troubleshooting

### View logs
```bash
docker-compose logs -f catalog-svc
```

### Check health
```bash
curl http://localhost:5001/health
```
