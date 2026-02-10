#!/bin/bash
# Gearify Dev Environment Deployment Script
# This script deploys the complete dev environment

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "========================================"
echo "Gearify Dev Environment Deployment"
echo "========================================"

# Check prerequisites
command -v aws >/dev/null 2>&1 || { echo "AWS CLI is required but not installed. Aborting." >&2; exit 1; }
command -v terraform >/dev/null 2>&1 || { echo "Terraform is required but not installed. Aborting." >&2; exit 1; }
command -v terragrunt >/dev/null 2>&1 || { echo "Terragrunt is required but not installed. Aborting." >&2; exit 1; }
command -v kubectl >/dev/null 2>&1 || { echo "kubectl is required but not installed. Aborting." >&2; exit 1; }
command -v helm >/dev/null 2>&1 || { echo "Helm is required but not installed. Aborting." >&2; exit 1; }

# Verify AWS credentials
echo "Verifying AWS credentials..."
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
echo "Using AWS Account: $AWS_ACCOUNT_ID"

# Step 1: Create Terraform State Backend
echo ""
echo "Step 1: Creating Terraform state backend..."
STATE_BUCKET="gearify-terraform-state-${AWS_ACCOUNT_ID}"
LOCK_TABLE="gearify-terraform-locks"

if ! aws s3api head-bucket --bucket "$STATE_BUCKET" 2>/dev/null; then
    echo "Creating S3 bucket for state: $STATE_BUCKET"
    aws s3 mb "s3://$STATE_BUCKET" --region us-east-1
    aws s3api put-bucket-versioning --bucket "$STATE_BUCKET" --versioning-configuration Status=Enabled
    aws s3api put-bucket-encryption --bucket "$STATE_BUCKET" --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'
else
    echo "State bucket already exists: $STATE_BUCKET"
fi

if ! aws dynamodb describe-table --table-name "$LOCK_TABLE" 2>/dev/null; then
    echo "Creating DynamoDB table for locks: $LOCK_TABLE"
    aws dynamodb create-table \
        --table-name "$LOCK_TABLE" \
        --attribute-definitions AttributeName=LockID,AttributeType=S \
        --key-schema AttributeName=LockID,KeyType=HASH \
        --billing-mode PAY_PER_REQUEST \
        --region us-east-1
else
    echo "Lock table already exists: $LOCK_TABLE"
fi

# Step 2: Deploy ECR Repositories (Global)
echo ""
echo "Step 2: Deploying ECR repositories..."
cd "$ROOT_DIR/gearify-infrastructure/global/ecr"
terragrunt init --terragrunt-non-interactive
terragrunt apply --terragrunt-non-interactive -auto-approve

# Step 3: Deploy VPC
echo ""
echo "Step 3: Deploying VPC..."
cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1/vpc"
terragrunt init --terragrunt-non-interactive
terragrunt apply --terragrunt-non-interactive -auto-approve

# Step 4: Deploy EKS
echo ""
echo "Step 4: Deploying EKS cluster..."
cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1/eks"
terragrunt init --terragrunt-non-interactive
terragrunt apply --terragrunt-non-interactive -auto-approve

# Configure kubectl
echo "Configuring kubectl..."
aws eks update-kubeconfig --name gearify-dev --region us-east-1

# Step 5: Deploy Data Stores
echo ""
echo "Step 5: Deploying data stores..."
cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1"

for module in rds elasticache dynamodb s3 sns-sqs secrets; do
    echo "Deploying $module..."
    cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1/$module"
    terragrunt init --terragrunt-non-interactive
    terragrunt apply --terragrunt-non-interactive -auto-approve
done

# Step 6: Deploy IRSA roles
echo ""
echo "Step 6: Deploying IRSA roles..."
cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1/irsa"
terragrunt init --terragrunt-non-interactive
terragrunt apply --terragrunt-non-interactive -auto-approve

# Step 7: Install cluster addons
echo ""
echo "Step 7: Installing cluster addons..."

cd "$ROOT_DIR/gearify-infrastructure/environments/dev/us-east-1/eks"
LB_CONTROLLER_ROLE=$(terragrunt output -raw aws_lb_controller_role_arn)
EXTERNAL_SECRETS_ROLE=$(terragrunt output -raw external_secrets_role_arn)

helm repo add eks https://aws.github.io/eks-charts
helm repo add external-secrets https://charts.external-secrets.io
helm repo update

helm upgrade --install aws-load-balancer-controller eks/aws-load-balancer-controller \
    -n kube-system \
    --set clusterName=gearify-dev \
    --set serviceAccount.create=true \
    --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"="$LB_CONTROLLER_ROLE"

helm upgrade --install external-secrets external-secrets/external-secrets \
    -n external-secrets \
    --create-namespace \
    --set serviceAccount.create=true \
    --set serviceAccount.annotations."eks\.amazonaws\.com/role-arn"="$EXTERNAL_SECRETS_ROLE"

kubectl wait --for=condition=available deployment/aws-load-balancer-controller -n kube-system --timeout=300s
kubectl wait --for=condition=available deployment/external-secrets -n external-secrets --timeout=300s

# Step 8: Apply Kubernetes infrastructure
echo ""
echo "Step 8: Applying Kubernetes infrastructure..."
kubectl apply -k "$ROOT_DIR/gearify-gitops/infrastructure/base/"

# Step 9: Install ArgoCD
echo ""
echo "Step 9: Installing ArgoCD..."
kubectl apply -k "$ROOT_DIR/gearify-gitops/bootstrap/argocd/"
kubectl wait --for=condition=available deployment/argocd-server -n argocd --timeout=300s

ARGOCD_PASSWORD=$(kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d)

# Step 10: Apply ArgoCD applications
echo ""
echo "Step 10: Applying ArgoCD applications..."
kubectl apply -f "$ROOT_DIR/gearify-gitops/argocd/projects/"
kubectl apply -f "$ROOT_DIR/gearify-gitops/argocd/apps/dev/"
kubectl apply -f "$ROOT_DIR/gearify-gitops/argocd/applicationsets/"

# Step 11: Deploy observability stack
echo ""
echo "Step 11: Deploying observability stack..."
kubectl apply -k "$ROOT_DIR/gearify-gitops/observability/base/"

echo ""
echo "========================================"
echo "Deployment Complete!"
echo "========================================"
echo ""
echo "ArgoCD UI: kubectl port-forward svc/argocd-server -n argocd 8080:443"
echo "ArgoCD Admin Password: $ARGOCD_PASSWORD"
echo ""
echo "Grafana UI: kubectl port-forward svc/grafana -n observability-dev 3000:3000"
echo "Jaeger UI: kubectl port-forward svc/jaeger-query -n observability-dev 16686:16686"
