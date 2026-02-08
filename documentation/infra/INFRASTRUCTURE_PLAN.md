# Gearify Infrastructure, CI/CD, GitOps & ArgoCD Design Document

## Overview

This document outlines the infrastructure and deployment strategy for the Gearify microservices platform on AWS using Terraform/Terragrunt, EKS, GitOps principles, and ArgoCD.

### Configuration Summary

| Setting | Value |
|---------|-------|
| **Cloud Provider** | AWS |
| **Environments** | Development, Staging, Production |
| **Repository Structure** | Application monorepo + Separate GitOps config repository |
| **CI/CD** | Basic (Build, Test, Deploy) |

---

## 1. Repository Structure

### 1.1 New Repositories to Create

```
gearify-infrastructure/     # Terraform/Terragrunt IaC
gearify-gitops/            # Kubernetes manifests, Helm charts, ArgoCD config
```

### 1.2 Infrastructure Repository Structure

```
gearify-infrastructure/
├── terragrunt.hcl                    # Root configuration
├── modules/                          # Reusable Terraform modules
│   ├── vpc/
│   ├── eks/
│   ├── rds/
│   ├── elasticache/
│   ├── sns-sqs/
│   ├── ecr/
│   ├── s3/
│   ├── secrets-manager/
│   ├── dynamodb/
│   └── opensearch/
├── environments/
│   ├── _envcommon/                   # Shared configuration
│   ├── dev/us-east-1/
│   ├── staging/us-east-1/
│   └── prod/us-east-1/
└── global/                           # ECR, IAM (shared across envs)
```

### 1.3 GitOps Repository Structure

```
gearify-gitops/
├── charts/                           # Helm charts
│   ├── gearify-service/             # Base chart for microservices
│   ├── gearify-web/
│   └── gearify-gateway/
├── bootstrap/                        # ArgoCD & cluster addons
│   ├── argocd/
│   └── cluster-addons/
├── infrastructure/                   # Cluster-wide resources
│   ├── base/
│   └── overlays/{dev,staging,prod}/
├── apps/                            # Application deployments
│   ├── base/{service}/
│   └── overlays/{dev,staging,prod}/{service}/
├── observability/                   # Monitoring stack
│   ├── base/
│   └── overlays/{dev,staging,prod}/
└── argocd/                          # ArgoCD Applications
    ├── projects/
    ├── applicationsets/
    └── apps/{dev,staging,prod}/
```

---

## 2. AWS Infrastructure (Terraform)

### 2.1 Components to Deploy

| Component | Dev | Staging | Prod |
|-----------|-----|---------|------|
| **VPC** | 10.0.0.0/16 | 10.1.0.0/16 | 10.2.0.0/16 |
| **EKS** | 1.29, t3.medium (2-5 nodes) | 1.29, t3.large (2-8 nodes) | 1.29, m5.large (3-15 nodes) |
| **RDS PostgreSQL** | db.t3.medium, single-AZ | db.t3.large, single-AZ | db.r5.large, multi-AZ |
| **ElastiCache Redis** | cache.t3.micro, 1 node | cache.t3.small, 1 node | cache.r5.large, 2 nodes |
| **NAT Gateway** | 1 (shared) | 1 (shared) | 3 (per AZ) |

### 2.2 SNS Topics

Based on existing LocalStack setup:

- `gearify-order-events-{env}`
- `gearify-payment-events-{env}`
- `gearify-shipping-events-{env}`
- `gearify-media-upload-events-{env}`
- `gearify-image-processing-completed-{env}`
- `catalog-events-topic-{env}`

### 2.3 SQS Queues (with filter policies)

| Queue | Topic | Filter |
|-------|-------|--------|
| gearify-order-created-queue | order-events | OrderCreatedEvent |
| gearify-order-cancelled-queue | order-events | OrderCancelledEvent |
| gearify-payment-completed-queue | payment-events | PaymentCompletedEvent |
| gearify-payment-failed-queue | payment-events | PaymentFailedEvent |
| gearify-refund-completed-queue | payment-events | RefundCompletedEvent |
| gearify-shipping-shipped-queue | shipping-events | ShippingShippedEvent |
| gearify-shipping-delivered-queue | shipping-events | ShippingDeliveredEvent |
| gearify-order-confirmed-shipping-queue | order-events | OrderConfirmedEvent |

### 2.4 ECR Repositories (13 total)

```
gearify-tenant-svc
gearify-catalog-svc
gearify-auth-svc
gearify-search-svc
gearify-cart-svc
gearify-order-svc
gearify-payment-svc
gearify-shipping-svc
gearify-inventory-svc
gearify-media-svc
gearify-notification-svc
gearify-api-gateway
gearify-web
```

---

## 3. Kubernetes Architecture

### 3.1 Namespace Strategy

| Namespace | Purpose |
|-----------|---------|
| `gearify-dev` | Development workloads |
| `gearify-staging` | Staging workloads |
| `gearify-prod` | Production workloads |
| `argocd` | ArgoCD components |
| `observability-{env}` | Monitoring stack per environment |

### 3.2 Base Helm Chart Features

The `gearify-service` base chart includes:

- Deployment with configurable replicas
- Service (ClusterIP)
- ServiceAccount with IRSA annotations
- HorizontalPodAutoscaler
- PodDisruptionBudget
- ConfigMap for environment variables
- ExternalSecret for AWS Secrets Manager integration
- ServiceMonitor for Prometheus scraping

### 3.3 Resource Defaults

| Environment | CPU Request | CPU Limit | Memory Request | Memory Limit | Replicas |
|-------------|-------------|-----------|----------------|--------------|----------|
| Dev | 50m | 250m | 128Mi | 256Mi | 1 |
| Staging | 100m | 500m | 256Mi | 512Mi | 2 |
| Prod | 250m | 1000m | 512Mi | 1Gi | 2-10 (HPA) |

---

## 4. ArgoCD Configuration

### 4.1 Projects

| Project | Purpose |
|---------|---------|
| `gearify-infrastructure` | Cluster-wide resources |
| `gearify-apps` | Microservice applications |
| `gearify-observability` | Monitoring stack |

### 4.2 ApplicationSet Strategy

Single ApplicationSet with matrix generator:

- **Environments**: dev, staging, prod
- **Services**: All 13 services

### 4.3 Sync Policies

| Environment | Auto Sync | Auto Prune | Self Heal |
|-------------|-----------|------------|-----------|
| Dev | Yes | Yes | Yes |
| Staging | Yes | Yes | Yes |
| Prod | No (manual) | No | No |

---

## 5. CI/CD Pipeline (GitHub Actions)

### 5.1 Workflow Structure

```
.github/workflows/
├── ci.yml                    # Master workflow with path detection
├── ci-microservice.yml       # Reusable .NET service workflow
├── ci-web.yml               # Angular frontend workflow
├── update-gitops.yml        # Update GitOps repo
└── promote-environment.yml  # Environment promotion
```

### 5.2 Pipeline Flow

```
Push to develop → Build & Test → Push to ECR → Update GitOps (dev) → ArgoCD auto-sync
Push to main → Build & Test → Push to ECR → Update GitOps (staging) → ArgoCD auto-sync
Manual promotion → Create PR → Merge → ArgoCD manual sync (prod)
```

### 5.3 Image Tagging Strategy

| Branch/Event | Tag Format |
|--------------|------------|
| Feature branches | `{branch}-{short-sha}` |
| Develop | `dev-{short-sha}` |
| Main | `{short-sha}` or `v{semver}` for tags |
| Latest | Updated per environment |

---

## 6. Secrets Management

### 6.1 AWS Secrets Manager Structure

```
gearify/{env}/database/orders     → PostgreSQL connection string
gearify/{env}/database/payments   → PostgreSQL connection string
gearify/{env}/database/shipping   → PostgreSQL connection string
gearify/{env}/redis               → Redis connection URL + auth token
gearify/{env}/jwt                 → JWT signing key
gearify/{env}/stripe              → Stripe API keys
```

### 6.2 External Secrets Operator

- ClusterSecretStore pointing to AWS Secrets Manager
- ExternalSecret per service pulling required secrets
- Refresh interval: 1 hour

---

## 7. Observability Stack

### 7.1 Components

| Component | Purpose | Storage |
|-----------|---------|---------|
| Prometheus | Metrics collection | 100Gi gp3 (prod) |
| Grafana | Visualization | 10Gi gp3 |
| Jaeger | Distributed tracing | OpenSearch |
| OTEL Collector | Trace/metric aggregation | N/A |
| AWS CloudWatch | Container Insights | Managed |

### 7.2 Integration Points

- All services instrumented with OpenTelemetry
- Prometheus scrapes `/metrics` endpoint
- Traces exported to Jaeger and AWS X-Ray
- Logs to CloudWatch Logs

---

## 8. Implementation Plan

### Phase 1: Foundation (Week 1-2)

- [ ] Create `gearify-infrastructure` repository
- [ ] Create `gearify-gitops` repository
- [ ] Set up Terraform state backend (S3 + DynamoDB)
- [ ] Deploy VPC for dev environment
- [ ] Create ECR repositories

### Phase 2: Core Infrastructure (Week 3-4)

- [ ] Deploy EKS cluster (dev)
- [ ] Deploy RDS PostgreSQL instances (dev)
- [ ] Deploy ElastiCache Redis (dev)
- [ ] Configure SNS/SQS (dev)
- [ ] Set up Secrets Manager

### Phase 3: Kubernetes Platform (Week 5-6)

- [ ] Install AWS Load Balancer Controller
- [ ] Install External Secrets Operator
- [ ] Install cert-manager
- [ ] Configure namespaces and resource quotas
- [ ] Set up ingress

### Phase 4: GitOps Setup (Week 7-8)

- [ ] Create base Helm charts
- [ ] Set up Kustomize overlays
- [ ] Install ArgoCD
- [ ] Configure ApplicationSets
- [ ] Deploy pilot service (catalog-svc)

### Phase 5: CI/CD Integration (Week 9-10)

- [ ] Create GitHub Actions workflows
- [ ] Test build and push pipeline
- [ ] Test GitOps update workflow
- [ ] Deploy all services to dev
- [ ] Test promotion workflow

### Phase 6: Staging & Production (Week 11-12)

- [ ] Deploy staging infrastructure
- [ ] Deploy production infrastructure
- [ ] Configure multi-environment ArgoCD
- [ ] Test full promotion pipeline

### Phase 7: Observability & Hardening (Week 13-14)

- [ ] Deploy observability stack
- [ ] Create Grafana dashboards
- [ ] Configure alerting
- [ ] Security review
- [ ] Documentation

---

## 9. Files to Create/Modify

### 9.1 New Files (gearify-infrastructure)

| File | Purpose |
|------|---------|
| `terragrunt.hcl` | Root Terragrunt config |
| `modules/vpc/main.tf` | VPC with subnets, NAT, endpoints |
| `modules/eks/main.tf` | EKS cluster with node groups, IRSA |
| `modules/rds/main.tf` | PostgreSQL with secrets |
| `modules/elasticache/main.tf` | Redis cluster |
| `modules/sns-sqs/main.tf` | Event-driven messaging |
| `modules/ecr/main.tf` | Container registries |
| `environments/dev/env.hcl` | Dev environment config |

### 9.2 New Files (gearify-gitops)

| File | Purpose |
|------|---------|
| `charts/gearify-service/` | Base Helm chart |
| `argocd/applicationsets/services-appset.yaml` | Multi-service deployment |
| `apps/base/{service}/values.yaml` | Per-service base config |
| `apps/overlays/{env}/{service}/values-patch.yaml` | Environment overrides |

### 9.3 Modified Files (gearify monorepo)

| File | Purpose |
|------|---------|
| `.github/workflows/ci.yml` | Master CI workflow |
| `.github/workflows/ci-microservice.yml` | Reusable workflow |
| Dockerfiles | Multi-stage optimization (if needed) |

---

## 10. Verification

### 10.1 Infrastructure Verification

```bash
# Verify EKS cluster
aws eks describe-cluster --name gearify-dev --region us-east-1

# Verify RDS instances
aws rds describe-db-instances --region us-east-1

# Verify ECR repositories
aws ecr describe-repositories --region us-east-1
```

### 10.2 ArgoCD Verification

```bash
# Port-forward ArgoCD
kubectl port-forward svc/argocd-server -n argocd 8080:443

# Check application sync status
argocd app list
argocd app get catalog-svc-dev
```

### 10.3 CI/CD Verification

1. Push a change to `gearify-catalog-svc` on develop branch
2. Verify GitHub Actions workflow runs successfully
3. Verify image pushed to ECR with correct tag
4. Verify GitOps repo updated with new image tag
5. Verify ArgoCD syncs the change to dev environment
6. Verify service is running with new version

---

## 11. Key Reference Files

| File | Purpose |
|------|---------|
| `gearify-umbrella/docker-compose.yml` | Current service configuration |
| `gearify-umbrella/localstack/scripts/init-sns.sh` | SNS/SQS setup to replicate |
| `gearify-umbrella/localstack/scripts/init-sqs.sh` | Queue configuration |
| `gearify-catalog-svc/Dockerfile` | Dockerfile template pattern |
| `gearify-api-gateway/appsettings.json` | Service routing config |

---

## 12. Architecture Diagrams

### 12.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              AWS Cloud                                   │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                      │
│  │     DEV     │  │   STAGING   │  │    PROD     │                      │
│  │  VPC        │  │  VPC        │  │  VPC        │                      │
│  │  10.0.0.0   │  │  10.1.0.0   │  │  10.2.0.0   │                      │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘                      │
│         │                │                │                              │
│  ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐                      │
│  │     EKS     │  │     EKS     │  │     EKS     │                      │
│  │   Cluster   │  │   Cluster   │  │   Cluster   │                      │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘                      │
│         │                │                │                              │
│         └────────────────┼────────────────┘                              │
│                          │                                               │
│  ┌───────────────────────▼───────────────────────┐                      │
│  │              Shared Services                   │                      │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────────┐   │                      │
│  │  │   ECR   │  │ Secrets │  │  SNS/SQS    │   │                      │
│  │  │         │  │ Manager │  │             │   │                      │
│  │  └─────────┘  └─────────┘  └─────────────┘   │                      │
│  └───────────────────────────────────────────────┘                      │
└─────────────────────────────────────────────────────────────────────────┘
```

### 12.2 GitOps Flow

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Developer  │    │   GitHub     │    │    ECR       │
│   Push Code  │───▶│   Actions    │───▶│  Container   │
└──────────────┘    └──────┬───────┘    └──────────────┘
                           │
                           ▼
                   ┌──────────────┐
                   │  GitOps Repo │
                   │ (Image Tags) │
                   └──────┬───────┘
                          │
                          ▼
                   ┌──────────────┐
                   │   ArgoCD     │
                   │   (Sync)     │
                   └──────┬───────┘
                          │
                          ▼
                   ┌──────────────┐
                   │     EKS      │
                   │   Cluster    │
                   └──────────────┘
```

### 12.3 Service Mesh

```
┌─────────────────────────────────────────────────────────────────┐
│                        EKS Cluster                               │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                    Ingress (ALB)                         │   │
│  └────────────────────────┬────────────────────────────────┘   │
│                           │                                      │
│  ┌────────────────────────▼────────────────────────────────┐   │
│  │                   API Gateway                            │   │
│  └────────────────────────┬────────────────────────────────┘   │
│                           │                                      │
│  ┌────────┬────────┬──────┴───┬────────┬────────┬────────┐    │
│  │        │        │          │        │        │        │     │
│  ▼        ▼        ▼          ▼        ▼        ▼        ▼     │
│ Auth   Catalog   Cart      Order   Payment  Shipping  Media   │
│ Svc     Svc      Svc        Svc      Svc      Svc      Svc    │
│  │        │        │          │        │        │        │     │
│  └────────┴────────┴──────────┴────────┴────────┴────────┘    │
│                           │                                      │
│  ┌────────────────────────▼────────────────────────────────┐   │
│  │              Data Layer (RDS, Redis, OpenSearch)         │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 13. Security Considerations

### 13.1 Network Security

- Private subnets for all workloads
- NAT Gateway for outbound internet access
- Security groups with least-privilege access
- VPC endpoints for AWS services (ECR, S3, Secrets Manager)

### 13.2 Access Control

- IRSA (IAM Roles for Service Accounts) for pod-level permissions
- RBAC for Kubernetes access
- GitHub OIDC for CI/CD authentication (no long-lived credentials)

### 13.3 Secrets

- All secrets in AWS Secrets Manager
- External Secrets Operator for Kubernetes integration
- No secrets in code or environment variables

### 13.4 Container Security

- Read-only root filesystem
- Non-root user execution
- Resource limits enforced
- Network policies for inter-service communication

---

## 14. Cost Optimization

### 14.1 Dev Environment

- Smaller instance types (t3.medium)
- Single NAT Gateway
- Single-AZ RDS
- Minimal node count

### 14.2 Production Environment

- Spot instances for non-critical workloads
- Reserved instances for baseline capacity
- Right-sizing based on metrics
- S3 lifecycle policies for logs

---

## 15. Disaster Recovery

### 15.1 Backup Strategy

| Component | Backup Method | Retention |
|-----------|---------------|-----------|
| RDS | Automated snapshots | 7 days (dev), 30 days (prod) |
| EKS | Velero | 7 days |
| Secrets Manager | Cross-region replication | N/A |
| S3 | Cross-region replication | N/A |

### 15.2 Recovery Objectives

| Environment | RTO | RPO |
|-------------|-----|-----|
| Dev | 4 hours | 24 hours |
| Staging | 2 hours | 12 hours |
| Prod | 1 hour | 1 hour |

---

## Appendix A: Environment Variables

### Common Variables (All Services)

```yaml
ASPNETCORE_ENVIRONMENT: "{Environment}"
AWS_REGION: "us-east-1"
OTEL_EXPORTER_OTLP_ENDPOINT: "http://otel-collector:4317"
```

### Service-Specific Variables

See individual service configurations in `apps/base/{service}/values.yaml`.

---

## Appendix B: Troubleshooting

### Common Issues

1. **Pod not starting**: Check resource limits and node capacity
2. **Secret not found**: Verify ExternalSecret sync status
3. **Service unreachable**: Check network policies and service discovery
4. **Image pull errors**: Verify ECR permissions and image tag

### Useful Commands

```bash
# Check pod logs
kubectl logs -f deployment/{service} -n gearify-{env}

# Check ArgoCD sync status
argocd app get {service}-{env}

# Force ArgoCD sync
argocd app sync {service}-{env}

# Check external secrets
kubectl get externalsecrets -n gearify-{env}
```
