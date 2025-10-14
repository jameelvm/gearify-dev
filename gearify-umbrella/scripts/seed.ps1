$ErrorActionPreference = "Stop"

Write-Host "🌱 Seeding databases..." -ForegroundColor Green

Write-Host "Seeding DynamoDB..." -ForegroundColor Cyan
node scripts/seed/seed-dynamodb.js

Write-Host "Seeding PostgreSQL..." -ForegroundColor Cyan
Get-Content scripts/seed/seed-postgres.sql | docker exec -i gearify-postgres psql -U postgres -d gearify

Write-Host "Seeding Redis..." -ForegroundColor Cyan
bash scripts/seed/seed-redis.sh

Write-Host "✅ Seeding complete!" -ForegroundColor Green
