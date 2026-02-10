#!/bin/bash
# Build and push all service images to ECR
# Usage: ./build-and-push.sh [service-name] [tag]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

AWS_REGION="${AWS_REGION:-us-east-1}"
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"

SERVICES=(
    "gearify-tenant-svc"
    "gearify-catalog-svc"
    "gearify-auth-svc"
    "gearify-search-svc"
    "gearify-cart-svc"
    "gearify-order-svc"
    "gearify-payment-svc"
    "gearify-shipping-svc"
    "gearify-inventory-svc"
    "gearify-media-svc"
    "gearify-notification-svc"
    "gearify-api-gateway"
    "gearify-web"
)

TARGET_SERVICE="${1:-all}"
TAG="${2:-dev-$(git rev-parse --short HEAD)}"

echo "========================================"
echo "Gearify Image Build and Push"
echo "========================================"
echo "ECR Registry: $ECR_REGISTRY"
echo "Tag: $TAG"
echo ""

aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $ECR_REGISTRY

build_and_push() {
    local service=$1
    local service_dir="$ROOT_DIR/$service"

    if [ ! -d "$service_dir" ] || [ ! -f "$service_dir/Dockerfile" ]; then
        echo "Warning: Skipping $service (not found)"
        return 0
    fi

    echo "Building $service..."
    docker build -t "$ECR_REGISTRY/$service:$TAG" "$service_dir"
    docker push "$ECR_REGISTRY/$service:$TAG"
    docker tag "$ECR_REGISTRY/$service:$TAG" "$ECR_REGISTRY/$service:dev-latest"
    docker push "$ECR_REGISTRY/$service:dev-latest"
    echo "$service done!"
}

if [ "$TARGET_SERVICE" == "all" ]; then
    for service in "${SERVICES[@]}"; do
        build_and_push "$service"
    done
else
    build_and_push "$TARGET_SERVICE"
fi

echo ""
echo "Build and Push Complete! Tag: $TAG"
