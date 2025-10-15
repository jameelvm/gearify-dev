# Health Check Script for Gearify Services
Write-Host "=== Gearify Health Check ===" -ForegroundColor Cyan
Write-Host ""

$services = @(
    @{Name="API Gateway"; Port=8080; Path="/health"},
    @{Name="Tenant Service"; Port=5008; Path="/health"},
    @{Name="Catalog Service"; Port=5001; Path="/health"},
    @{Name="Search Service"; Port=5002; Path="/health"},
    @{Name="Cart Service"; Port=5003; Path="/health"},
    @{Name="Order Service"; Port=5004; Path="/health"},
    @{Name="Payment Service"; Port=5005; Path="/health"},
    @{Name="Shipping Service"; Port=5006; Path="/health"},
    @{Name="Inventory Service"; Port=5007; Path="/health"},
    @{Name="Media Service"; Port=5009; Path="/health"},
    @{Name="Notification Service"; Port=5010; Path="/health"},
    @{Name="Web Application"; Port=4200; Path="/"}
)

$healthy = 0
$unhealthy = 0

foreach ($service in $services) {
    $url = "http://localhost:$($service.Port)$($service.Path)"
    try {
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] $($service.Name) - Port $($service.Port)" -ForegroundColor Green
            $healthy++
        } else {
            Write-Host "[WARN] $($service.Name) - Port $($service.Port) - Status: $($response.StatusCode)" -ForegroundColor Yellow
            $unhealthy++
        }
    } catch {
        Write-Host "[ERROR] $($service.Name) - Port $($service.Port) - Not responding" -ForegroundColor Red
        $unhealthy++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Healthy: $healthy" -ForegroundColor Green
Write-Host "Unhealthy: $unhealthy" -ForegroundColor Red
Write-Host ""

# Check Docker containers
Write-Host "=== Docker Container Status ===" -ForegroundColor Cyan
docker compose ps --format "table {{.Service}}\t{{.Status}}" | Select-String -Pattern "api-gateway|catalog-svc|search-svc|cart-svc|order-svc|payment-svc|shipping-svc|inventory-svc|media-svc|notification-svc|tenant-svc|web"
