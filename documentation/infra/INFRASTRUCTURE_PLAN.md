# Gearify Infrastructure, CI/CD, GitOps & ArgoCD Design Document

## Overview

This document outlines the infrastructure and deployment strategy for the Gearify microservices platform on AWS using Terraform/Terragrunt, EKS, GitOps principles, and ArgoCD.

### Configuration Summary

| Setting | Value |
|---------|-------|
| **Cloud Provider** | AWS |
| **Environments** | Development (focus), Staging, Production |
| **Repository Structure** | Application monorepo + Separate GitOps config |
| **CI/CD** | GitHub Actions |

---

## Quick Start (Dev Environment)

```bash
# 1. Run the deployment script
./scripts/deploy-dev.sh

# 2. Build and push images
./scripts/build-and-push.sh all

# 3. Access ArgoCD
kubectl port-forward svc/argocd-server -n argocd 8080:443

# 4. Access Grafana
kubectl port-forward svc/grafana -n observability-dev 3000:3000
```

---

## Repository Structure

### Infrastructure Repository (`gearify-infrastructure/`)

```
gearify-infrastructure/
├── terragrunt.hcl                    # Root configuration
├── modules/
│   ├── vpc/                         # VPC with subnets, NAT, endpoints
│   ├── eks/                         # EKS cluster with node groups, IRSA
│   ├── rds/                         # PostgreSQL database
│   ├── elasticache/                 # Redis cluster
│   ├── sns-sqs/                     # Event-driven messaging
│   ├── ecr/                         # Container registries
│   ├── s3/                          # Object storage
│   ├── dynamodb/                    # NoSQL tables
│   ├── secrets-manager/             # Secrets management
│   ├── opensearch/                  # Search service
│   └── irsa/                        # Service IAM roles
├── environments/
│   ├── _envcommon/                  # Shared configuration
│   └── dev/us-east-1/               # Dev environment
└── global/
    └── ecr/                         # ECR repositories (shared)
```

### GitOps Repository (`gearify-gitops/`)

```
gearify-gitops/
├── charts/
│   └── gearify-service/             # Base Helm chart
├── bootstrap/
│   ├── argocd/                      # ArgoCD installation
│   └── cluster-addons/              # AWS LB Controller, External Secrets
├── infrastructure/
│   └── base/                        # Namespaces, quotas, network policies
├── apps/
│   ├── base/                        # Base service configs
│   └── overlays/dev/                # Dev environment overrides
├── observability/
│   └── base/                        # Prometheus, Grafana, Jaeger, OTEL
└── argocd/
    ├── projects/                    # ArgoCD projects
    ├── applicationsets/             # Service deployment
    └── apps/dev/                    # Dev applications
```

---

## AWS Components (Dev Environment)

| Component | Configuration |
|-----------|--------------|
| **VPC** | 10.0.0.0/16, 3 AZs |
| **EKS** | v1.29, t3.medium (2-5 nodes) |
| **RDS** | PostgreSQL 16, db.t3.medium |
| **ElastiCache** | Redis 7.1, cache.t3.micro |
| **NAT Gateway** | 1 (shared) |
| **OpenSearch** | t3.small.search, 20GB |

---

## Services (13 total)

| Service | Port | Storage |
|---------|------|---------|
| api-gateway | 8080 | - |
| tenant-svc | 5008 | DynamoDB |
| catalog-svc | 5001 | DynamoDB, Redis |
| auth-svc | 5011 | DynamoDB, Redis |
| search-svc | 5012 | OpenSearch |
| cart-svc | 5013 | Redis |
| order-svc | 5014 | PostgreSQL |
| payment-svc | 5015 | PostgreSQL |
| shipping-svc | 5006 | PostgreSQL |
| inventory-svc | 5007 | DynamoDB |
| media-svc | 5009 | S3, DynamoDB |
| notification-svc | 5010 | SES |
| web | 80 | - |

---

## Event-Driven Architecture

### SNS Topics
- `gearify-order-events-dev`
- `gearify-payment-events-dev`
- `gearify-shipping-events-dev`
- `gearify-media-upload-events-dev`
- `gearify-image-processing-completed-dev`
- `gearify-catalog-events-dev`

### SQS Queues (with filter policies)
- Order: `order-created`, `order-cancelled`, `order-confirmed-shipping`
- Payment: `payment-completed`, `payment-failed`, `refund-completed`
- Shipping: `shipping-shipped`, `shipping-delivered`
- Media: `image-processing`, `product-thumbnail-update`
- Search: `search-catalog-events`

---

## CI/CD Pipeline

### Workflows (`.github/workflows/`)
- `ci.yml` - Master workflow with change detection
- `ci-microservice.yml` - Reusable .NET workflow
- `promote-environment.yml` - Environment promotion
- `deploy-infrastructure.yml` - Terraform deployment

### Flow
```
Push to develop → Build & Test → Push to ECR → Update GitOps → ArgoCD sync
```

---

## Secrets Management

Secrets are stored in AWS Secrets Manager:
- `gearify/dev/database/main` - PostgreSQL credentials
- `gearify/dev/redis` - Redis connection
- `gearify/dev/jwt` - JWT signing key
- `gearify/dev/stripe` - Stripe API keys
- `gearify/dev/smtp` - Email credentials
- `gearify/dev/opensearch` - OpenSearch credentials

---

## Observability Stack

| Component | Purpose |
|-----------|---------|
| Prometheus | Metrics collection |
| Grafana | Visualization |
| Jaeger | Distributed tracing |
| OTEL Collector | Trace/metric aggregation |

---

## Files Created

### Infrastructure (Terraform)
- 10 modules (VPC, EKS, RDS, ElastiCache, SNS-SQS, ECR, S3, DynamoDB, Secrets, OpenSearch, IRSA)
- Dev environment Terragrunt configs
- Global ECR config

### GitOps (Kubernetes)
- Base Helm chart with 11 templates
- 13 service overlays for dev
- ArgoCD projects and ApplicationSets
- Observability stack (Prometheus, Grafana, Jaeger, OTEL)
- Network policies and resource quotas

### CI/CD (GitHub Actions)
- 4 workflow files
- Change detection for monorepo
- GitOps update automation

### Scripts
- `deploy-dev.sh` - Full deployment script
- `build-and-push.sh` - Docker image builder
- `update-gitops.sh` - GitOps tag updater
