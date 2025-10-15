#!/bin/bash

echo "=== Gearify Services Health Check ==="
echo ""

# Infrastructure services
echo "Infrastructure Services:"
echo "======================="
docker compose ps postgres redis localstack | tail -n +2

echo ""
echo "Observability Services:"
echo "======================="
docker compose ps seq jaeger prometheus grafana otel-collector mailhog | tail -n +2

echo ""
echo "Application Services:"
echo "===================="
docker compose ps tenant-svc catalog-svc search-svc cart-svc order-svc payment-svc shipping-svc inventory-svc media-svc notification-svc api-gateway web | tail -n +2

echo ""
echo "=== Port Accessibility Check ==="
echo "================================"

# Check if ports are accessible
check_port() {
    local name=$1
    local port=$2
    timeout 2 bash -c "cat < /dev/null > /dev/tcp/localhost/$port" 2>/dev/null
    if [ $? -eq 0 ]; then
        echo "[OK] $name (port $port) is accessible"
    else
        echo "[ERROR] $name (port $port) is not accessible"
    fi
}

check_port "API Gateway" 8080
check_port "Web Application" 4200
check_port "Catalog Service" 5001
check_port "Search Service" 5002
check_port "Cart Service" 5003
check_port "Order Service" 5004
check_port "Payment Service" 5005
check_port "Shipping Service" 5006
check_port "Inventory Service" 5007
check_port "Tenant Service" 5008
check_port "Media Service" 5009
check_port "Notification Service" 5010

echo ""
echo "=== Container Logs Check (Last 5 lines) ==="
echo "==========================================="
for service in cart-svc api-gateway tenant-svc; do
    echo ""
    echo "--- $service ---"
    docker logs $service --tail 5 2>&1 | grep -i "error\|exception\|started\|listening" || echo "No notable logs"
done
