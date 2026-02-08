# Gearify Infrastructure Quick Start Guide

## Prerequisites

- AWS CLI configured with appropriate credentials
- Terraform >= 1.5.0
- Terragrunt >= 0.50.0
- kubectl
- Helm 3.x
- Docker

## Step 1: Deploy Infrastructure

### Option A: Automated Deployment

```bash
# Make scripts executable
chmod +x scripts/*.sh

# Run full deployment
./scripts/deploy-dev.sh
```

### Option B: Manual Step-by-Step

```bash
# 1. Create state backend
aws s3 mb s3://gearify-terraform-state-$(aws sts get-caller-identity --query Account --output text)
aws dynamodb create-table \
    --table-name gearify-terraform-locks \
    --attribute-definitions AttributeName=LockID,AttributeType=S \
    --key-schema AttributeName=LockID,KeyType=HASH \
    --billing-mode PAY_PER_REQUEST

# 2. Deploy ECR (global)
cd gearify-infrastructure/global/ecr
terragrunt apply

# 3. Deploy dev infrastructure
cd ../environments/dev/us-east-1
terragrunt run-all apply
```

## Step 2: Configure kubectl

```bash
aws eks update-kubeconfig --name gearify-dev --region us-east-1
kubectl get nodes  # Verify connection
```

## Step 3: Install Cluster Addons

```bash
# AWS Load Balancer Controller
helm repo add eks https://aws.github.io/eks-charts
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
    -n kube-system \
    --set clusterName=gearify-dev

# External Secrets Operator
helm repo add external-secrets https://charts.external-secrets.io
helm install external-secrets external-secrets/external-secrets \
    -n external-secrets --create-namespace
```

## Step 4: Deploy ArgoCD

```bash
kubectl apply -k gearify-gitops/bootstrap/argocd/

# Wait for ArgoCD
kubectl wait --for=condition=available deployment/argocd-server -n argocd --timeout=300s

# Get admin password
kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
```

## Step 5: Apply ArgoCD Applications

```bash
kubectl apply -k gearify-gitops/infrastructure/base/
kubectl apply -f gearify-gitops/argocd/projects/
kubectl apply -f gearify-gitops/argocd/applicationsets/
```

## Step 6: Build and Push Images

```bash
# Build all services
./scripts/build-and-push.sh all

# Or build specific service
./scripts/build-and-push.sh gearify-catalog-svc
```

## Step 7: Update GitOps and Deploy

```bash
# Update image tags
./scripts/update-gitops.sh dev-abc123

# Commit and push
cd gearify-gitops
git add . && git commit -m "Deploy new version" && git push
```

## Accessing Services

### ArgoCD UI
```bash
kubectl port-forward svc/argocd-server -n argocd 8080:443
# Open https://localhost:8080
```

### Grafana
```bash
kubectl port-forward svc/grafana -n observability-dev 3000:3000
# Open http://localhost:3000 (admin/admin)
```

### Jaeger
```bash
kubectl port-forward svc/jaeger-query -n observability-dev 16686:16686
# Open http://localhost:16686
```

### Prometheus
```bash
kubectl port-forward svc/prometheus -n observability-dev 9090:9090
# Open http://localhost:9090
```

## Troubleshooting

### Check pod status
```bash
kubectl get pods -n gearify-dev
kubectl describe pod <pod-name> -n gearify-dev
kubectl logs <pod-name> -n gearify-dev
```

### Check ArgoCD sync status
```bash
kubectl get applications -n argocd
argocd app get <app-name>
argocd app sync <app-name>
```

### Check external secrets
```bash
kubectl get externalsecrets -n gearify-dev
kubectl describe externalsecret <name> -n gearify-dev
```

## Cleanup

```bash
# Delete Kubernetes resources
kubectl delete -f gearify-gitops/argocd/applicationsets/
kubectl delete -f gearify-gitops/argocd/projects/
kubectl delete -k gearify-gitops/bootstrap/argocd/

# Destroy infrastructure
cd gearify-infrastructure/environments/dev/us-east-1
terragrunt run-all destroy
```
