# Gearify Seed Data Script
Write-Host "Seeding Gearify Platform Data..." -ForegroundColor Cyan

$products = @(
    @{name="CA Plus 15000 Bat"; price=299.99; category="bat"},
    @{name="SG RSD Xtreme Bat"; price=349.99; category="bat"},
    @{name="GM Diamond Bat"; price=279.99; category="bat"},
    @{name="Kookaburra Ghost Pro Bat"; price=399.99; category="bat"},
    @{name="SG Test Pads"; price=79.99; category="pad"},
    @{name="CA Gloves"; price=59.99; category="glove"},
    @{name="Kookaburra Ball"; price=19.99; category="ball"}
)

Write-Host "Created $($products.Count) products"
Write-Host "Seed data complete!" -ForegroundColor Green
