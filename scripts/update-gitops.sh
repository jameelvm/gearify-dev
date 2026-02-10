#!/bin/bash
# Update GitOps repository with new image tags
# Usage: ./update-gitops.sh <tag> [environment] [service]

set -e

TAG="${1:?Tag is required}"
ENVIRONMENT="${2:-dev}"
SERVICE="${3:-all}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
GITOPS_DIR="$ROOT_DIR/gearify-gitops"

SERVICES=(
    "tenant-svc" "catalog-svc" "auth-svc" "search-svc" "cart-svc"
    "order-svc" "payment-svc" "shipping-svc" "inventory-svc"
    "media-svc" "notification-svc" "api-gateway" "web"
)

echo "Updating GitOps: Tag=$TAG, Env=$ENVIRONMENT, Service=$SERVICE"

update_service() {
    local svc=$1
    local values_file="$GITOPS_DIR/apps/overlays/$ENVIRONMENT/$svc/values.yaml"

    if [ ! -f "$values_file" ]; then
        echo "Warning: $values_file not found"
        return 0
    fi

    echo "Updating $svc..."
    if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' "s|tag: \".*\"|tag: \"$TAG\"|g" "$values_file"
    else
        sed -i "s|tag: \".*\"|tag: \"$TAG\"|g" "$values_file"
    fi
}

if [ "$SERVICE" == "all" ]; then
    for svc in "${SERVICES[@]}"; do
        update_service "$svc"
    done
else
    update_service "$SERVICE"
fi

echo "GitOps updated! Commit and push to deploy."
