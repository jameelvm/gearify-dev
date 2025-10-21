# Run Catalog Service in Development Mode with LocalStack
# This script ensures ASPNETCORE_ENVIRONMENT is set to Development

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Gearify Catalog Service - LocalStack Mode" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Set environment to Development
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Environment Settings:" -ForegroundColor Yellow
Write-Host "  ASPNETCORE_ENVIRONMENT: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Green
Write-Host ""

# Check if LocalStack is running
Write-Host "Checking LocalStack..." -ForegroundColor Yellow
try {
    $healthCheck = Invoke-RestMethod -Uri "http://localhost:4566/_localstack/health" -TimeoutSec 3
    Write-Host "  ✓ LocalStack is running" -ForegroundColor Green
    Write-Host "  ✓ DynamoDB status: $($healthCheck.services.dynamodb)" -ForegroundColor Green
} catch {
    Write-Host "  ✗ LocalStack is NOT running!" -ForegroundColor Red
    Write-Host "  Please start LocalStack first:" -ForegroundColor Yellow
    Write-Host "    docker run --rm -p 4566:4566 localstack/localstack" -ForegroundColor White
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit 1
}
Write-Host ""

# Check if DynamoDB table exists
Write-Host "Checking DynamoDB table..." -ForegroundColor Yellow
try {
    $headers = @{
        "Content-Type" = "application/x-amz-json-1.0"
        "X-Amz-Target" = "DynamoDB_20120810.ListTables"
    }
    $response = Invoke-RestMethod -Uri "http://localhost:4566/" -Method Post -Headers $headers -Body "{}"

    if ($response.TableNames -contains "gearify-products") {
        Write-Host "  ✓ Table 'gearify-products' exists" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Table 'gearify-products' NOT found!" -ForegroundColor Red
        Write-Host "  Available tables: $($response.TableNames -join ', ')" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ✗ Could not check DynamoDB tables" -ForegroundColor Red
}
Write-Host ""

# Run the service
Write-Host "Starting Catalog Service..." -ForegroundColor Yellow
Write-Host "Watch for these diagnostic messages:" -ForegroundColor Cyan
Write-Host "  - UseLocalStack: True" -ForegroundColor White
Write-Host "  - LocalStackHost: localhost:4566" -ForegroundColor White
Write-Host "  - AWS Options - ServiceURL: http://localhost:4566" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop the service" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Run the service
Set-Location C:\Gearify
dotnet run --project gearify-catalog-svc
