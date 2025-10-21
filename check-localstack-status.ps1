# Quick LocalStack Status Checker
# Run this to verify LocalStack is ready before starting the service

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "LocalStack Status Check" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check if LocalStack is running
Write-Host "[1/3] Checking if LocalStack is running..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:4566/_localstack/health" -TimeoutSec 3 -ErrorAction Stop
    Write-Host "      ✓ LocalStack is RUNNING" -ForegroundColor Green
    Write-Host "      Version: $($health.version)" -ForegroundColor Gray
    Write-Host "      Edition: $($health.edition)" -ForegroundColor Gray
} catch {
    Write-Host "      ✗ LocalStack is NOT running!" -ForegroundColor Red
    Write-Host ""
    Write-Host "To start LocalStack, run:" -ForegroundColor Yellow
    Write-Host "  docker run --rm -p 4566:4566 localstack/localstack" -ForegroundColor White
    Write-Host ""
    exit 1
}
Write-Host ""

# 2. Check DynamoDB service status
Write-Host "[2/3] Checking DynamoDB service..." -ForegroundColor Yellow
if ($health.services.dynamodb -eq "running") {
    Write-Host "      ✓ DynamoDB is RUNNING" -ForegroundColor Green
} else {
    Write-Host "      ✗ DynamoDB status: $($health.services.dynamodb)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 3. Check if gearify-products table exists
Write-Host "[3/3] Checking DynamoDB tables..." -ForegroundColor Yellow
try {
    $headers = @{
        "Content-Type" = "application/x-amz-json-1.0"
        "X-Amz-Target" = "DynamoDB_20120810.ListTables"
    }
    $response = Invoke-RestMethod -Uri "http://localhost:4566/" -Method Post -Headers $headers -Body "{}" -ErrorAction Stop

    if ($response.TableNames -contains "gearify-products") {
        Write-Host "      ✓ Table 'gearify-products' EXISTS" -ForegroundColor Green

        # Get item count
        $describeHeaders = @{
            "Content-Type" = "application/x-amz-json-1.0"
            "X-Amz-Target" = "DynamoDB_20120810.DescribeTable"
        }
        $describeBody = @{ TableName = "gearify-products" } | ConvertTo-Json
        $tableInfo = Invoke-RestMethod -Uri "http://localhost:4566/" -Method Post -Headers $describeHeaders -Body $describeBody -ErrorAction Stop

        Write-Host "      Items in table: $($tableInfo.Table.ItemCount)" -ForegroundColor Gray
        Write-Host "      Table status: $($tableInfo.Table.TableStatus)" -ForegroundColor Gray
    } else {
        Write-Host "      ✗ Table 'gearify-products' NOT found!" -ForegroundColor Red
        Write-Host "      Available tables: $($response.TableNames -join ', ')" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "You may need to run the database initialization script." -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "      ✗ Could not check DynamoDB tables" -ForegroundColor Red
    Write-Host "      Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# All checks passed
Write-Host "=====================================" -ForegroundColor Green
Write-Host "✓ All checks passed!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "LocalStack is ready. You can now run:" -ForegroundColor Cyan
Write-Host "  .\run-catalog-dev.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Or manually:" -ForegroundColor Cyan
Write-Host "  `$env:ASPNETCORE_ENVIRONMENT=`"Development`"" -ForegroundColor White
Write-Host "  dotnet run --project gearify-catalog-svc" -ForegroundColor White
Write-Host ""
