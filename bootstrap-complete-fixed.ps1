# Gearify Platform - Final Components (FIXED)
# Adds inventory, tenancy, checkout, auth, admin dashboard
# Run AFTER bootstrap-gearify-fixed.ps1 and bootstrap-enhancements-fixed.ps1

param(
    [string]$BaseDir = "C:\Gearify"
)

$ErrorActionPreference = "Stop"
Set-Location $BaseDir

function Write-FileContent {
    param([string]$Path, [string]$Content)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $Content | Out-File -FilePath $Path -Encoding UTF8 -Force
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Gearify Platform - Final Components" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# INVENTORY SERVICE
Write-Host "[1/8] Creating inventory service..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-inventory-svc"

$inventoryCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.InventoryService.csproj" $inventoryCsprojContent

$inventoryDomainContent = @'
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.InventoryService.Domain;

[DynamoDBTable("gearify-inventory")]
public class InventoryItem
{
    [DynamoDBHashKey]
    public string ProductId { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[DynamoDBTable("gearify-reservations")]
public class StockReservation
{
    [DynamoDBHashKey]
    public string ReservationId { get; set; } = Guid.NewGuid().ToString();
    public string ProductId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
    public string Status { get; set; } = "active";
}
'@
Write-FileContent "Domain/InventoryItem.cs" $inventoryDomainContent

$reserveStockContent = @'
using Amazon.DynamoDBv2.DataModel;
using Gearify.InventoryService.Domain;
using MediatR;

namespace Gearify.InventoryService.Application.Commands;

public record ReserveStockCommand(string ProductId, int Quantity, string OrderId) : IRequest<Result>;

public record Result(bool Success, string? ReservationId, string? Error);

public class ReserveStockHandler : IRequestHandler<ReserveStockCommand, Result>
{
    private readonly IDynamoDBContext _context;

    public ReserveStockHandler(IDynamoDBContext context) => _context = context;

    public async Task<Result> Handle(ReserveStockCommand cmd, CancellationToken ct)
    {
        var inventory = await _context.LoadAsync<InventoryItem>(cmd.ProductId, ct);

        if (inventory == null || inventory.AvailableQuantity < cmd.Quantity)
        {
            return new Result(false, null, "Insufficient stock");
        }

        inventory.AvailableQuantity -= cmd.Quantity;
        inventory.ReservedQuantity += cmd.Quantity;
        await _context.SaveAsync(inventory, ct);

        var reservation = new StockReservation
        {
            ProductId = cmd.ProductId,
            OrderId = cmd.OrderId,
            Quantity = cmd.Quantity
        };

        await _context.SaveAsync(reservation, ct);

        return new Result(true, reservation.ReservationId, null);
    }
}
'@
Write-FileContent "Application/Commands/ReserveStockCommand.cs" $reserveStockContent

# TENANT SERVICE
Write-Host "[2/8] Creating tenant service..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-tenant-svc"

$tenantDomainContent = @'
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.TenantService.Domain;

[DynamoDBTable("gearify-tenants")]
public class Tenant
{
    [DynamoDBHashKey]
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Dictionary<string, bool> FeatureFlags { get; set; } = new();
}
'@
Write-FileContent "Domain/Tenant.cs" $tenantDomainContent

# CHECKOUT COMPONENT
Write-Host "[3/8] Adding checkout to Angular..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-web"

$checkoutComponentContent = @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="checkout-container">
      <h2>Checkout</h2>

      <div class="checkout-steps">
        <div class="step" [class.active]="currentStep === 1">1. Shipping</div>
        <div class="step" [class.active]="currentStep === 2">2. Payment</div>
        <div class="step" [class.active]="currentStep === 3">3. Review</div>
      </div>

      <div *ngIf="currentStep === 1" class="step-content">
        <h3>Shipping Address</h3>
        <form [formGroup]="shippingForm">
          <input formControlName="firstName" placeholder="First Name" />
          <input formControlName="lastName" placeholder="Last Name" />
          <input formControlName="street" placeholder="Street Address" />
          <input formControlName="city" placeholder="City" />
          <button (click)="nextStep()" [disabled]="!shippingForm.valid">Continue</button>
        </form>
      </div>

      <div *ngIf="currentStep === 2" class="step-content">
        <h3>Payment Method</h3>
        <button (click)="selectPayment('stripe')">Credit Card</button>
        <button (click)="selectPayment('paypal')">PayPal</button>
        <button (click)="nextStep()">Review Order</button>
      </div>

      <div *ngIf="currentStep === 3" class="step-content">
        <h3>Review Your Order</h3>
        <div class="order-summary">
          <p>Total: $299.99</p>
        </div>
        <button (click)="placeOrder()">Place Order</button>
      </div>
    </div>
  `,
  styles: [`
    .checkout-container { max-width: 800px; margin: 0 auto; padding: 2rem; }
    .checkout-steps { display: flex; justify-content: space-between; margin-bottom: 2rem; }
    .step { flex: 1; padding: 1rem; background: #f5f5f5; text-align: center; }
    .step.active { background: #1e3a8a; color: white; }
    form input { display: block; width: 100%; padding: 0.75rem; margin-bottom: 1rem; }
    button { padding: 0.75rem 1.5rem; background: #1e3a8a; color: white; border: none; cursor: pointer; }
  `]
})
export class CheckoutComponent {
  currentStep = 1;
  selectedPayment = '';
  shippingForm: FormGroup;

  constructor(private fb: FormBuilder) {
    this.shippingForm = this.fb.group({
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      street: ['', Validators.required],
      city: ['', Validators.required]
    });
  }

  selectPayment(method: string) {
    this.selectedPayment = method;
  }

  nextStep() {
    if (this.currentStep < 3) this.currentStep++;
  }

  placeOrder() {
    alert('Order placed successfully!');
  }
}
'@
Write-FileContent "src/app/features/checkout/checkout.component.ts" $checkoutComponentContent

# PRODUCT DETAIL
Write-Host "[4/8] Adding product detail page..." -ForegroundColor Yellow

$productDetailContent = @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="product-detail">
      <div class="product-images">
        <img src="/assets/bat.jpg" alt="Cricket Bat" class="main-image" />
      </div>

      <div class="product-info">
        <h1>{{ product.name }}</h1>
        <div class="rating">⭐⭐⭐⭐⭐</div>
        <div class="price">Price: {{ product.price | currency }}</div>

        <div class="specifications">
          <h3>Specifications</h3>
          <table>
            <tr><td>Brand:</td><td>{{ product.brand }}</td></tr>
            <tr><td>Weight:</td><td>{{ product.weight }}</td></tr>
            <tr><td>Grade:</td><td>{{ product.grade }}</td></tr>
          </table>
        </div>

        <button (click)="addToCart()">Add to Cart</button>
      </div>
    </div>

    <div class="reviews-section">
      <h2>Customer Reviews</h2>
      <div class="review" *ngFor="let review of reviews">
        <p><strong>{{ review.author }}</strong></p>
        <p>{{ review.text }}</p>
      </div>
    </div>
  `,
  styles: [`
    .product-detail { display: grid; grid-template-columns: 1fr 1fr; gap: 2rem; padding: 2rem; }
    .main-image { width: 100%; }
    .price { font-size: 2rem; color: #1e3a8a; margin: 1rem 0; }
    .specifications table { width: 100%; margin: 1rem 0; }
    .specifications td { padding: 0.5rem; }
    button { padding: 1rem 2rem; background: #1e3a8a; color: white; border: none; cursor: pointer; }
    .reviews-section { padding: 2rem; }
    .review { border-bottom: 1px solid #eee; padding: 1rem 0; }
  `]
})
export class ProductDetailComponent {
  product = {
    name: 'CA Plus 15000 Bat',
    price: 299.99,
    brand: 'CA',
    weight: '35oz',
    grade: 'Grade 1'
  };

  reviews = [
    { author: 'John D.', text: 'Excellent bat! Great balance.' },
    { author: 'Raj P.', text: 'Best bat I have owned.' }
  ];

  addToCart() {
    alert('Added to cart!');
  }
}
'@
Write-FileContent "src/app/features/product/product-detail.component.ts" $productDetailContent

# AUTH SERVICE
Write-Host "[5/8] Adding authentication..." -ForegroundColor Yellow

$authServiceContent = @'
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

export interface User {
  id: string;
  email: string;
  name: string;
  role: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private currentUserSubject = new BehaviorSubject<User | null>(null);
  public currentUser$: Observable<User | null> = this.currentUserSubject.asObservable();

  login(email: string, password: string): boolean {
    const user: User = {
      id: '1',
      email: email,
      name: 'John Doe',
      role: 'customer'
    };
    this.currentUserSubject.next(user);
    localStorage.setItem('auth_token', 'fake-jwt-token');
    return true;
  }

  logout() {
    this.currentUserSubject.next(null);
    localStorage.removeItem('auth_token');
  }

  isAuthenticated(): boolean {
    return this.currentUserSubject.value !== null;
  }
}
'@
Write-FileContent "src/app/services/auth.service.ts" $authServiceContent

$authGuardContent = @'
import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};
'@
Write-FileContent "src/app/guards/auth.guard.ts" $authGuardContent

# ADMIN DASHBOARD
Write-Host "[6/8] Creating admin dashboard..." -ForegroundColor Yellow

$adminDashboardContent = @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="admin-dashboard">
      <h1>Admin Dashboard</h1>

      <div class="stats-grid">
        <div class="stat-card">
          <h3>Total Orders</h3>
          <div class="stat-value">1,247</div>
        </div>

        <div class="stat-card">
          <h3>Revenue</h3>
          <div class="stat-value">42,891 USD</div>
        </div>

        <div class="stat-card">
          <h3>Active Products</h3>
          <div class="stat-value">156</div>
        </div>

        <div class="stat-card">
          <h3>Customers</h3>
          <div class="stat-value">3,421</div>
        </div>
      </div>

      <div class="recent-orders">
        <h2>Recent Orders</h2>
        <table>
          <thead>
            <tr>
              <th>Order ID</th>
              <th>Customer</th>
              <th>Amount</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let order of recentOrders">
              <td>{{ order.id }}</td>
              <td>{{ order.customer }}</td>
              <td>{{ order.amount | currency }}</td>
              <td>{{ order.status }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .admin-dashboard { padding: 2rem; }
    .stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1.5rem; margin-bottom: 2rem; }
    .stat-card { background: white; padding: 1.5rem; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
    .stat-value { font-size: 2rem; font-weight: bold; color: #1e3a8a; }
    table { width: 100%; border-collapse: collapse; }
    th, td { padding: 0.75rem; text-align: left; border-bottom: 1px solid #eee; }
    th { background: #f9f9f9; }
  `]
})
export class AdminDashboardComponent {
  recentOrders = [
    { id: 'ORD-001', customer: 'John Doe', amount: 299.99, status: 'confirmed' },
    { id: 'ORD-002', customer: 'Jane Smith', amount: 159.98, status: 'pending' },
    { id: 'ORD-003', customer: 'Mike Johnson', amount: 449.99, status: 'confirmed' }
  ];
}
'@
Write-FileContent "src/app/features/admin/admin-dashboard.component.ts" $adminDashboardContent

# MONITORING
Write-Host "[7/8] Adding monitoring stack..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-umbrella"

$composeMonitoringContent = @'
version: '3.8'
services:
  prometheus:
    image: prom/prometheus
    ports:
      - '9090:9090'
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml

  grafana:
    image: grafana/grafana
    ports:
      - '3000:3000'
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
'@
Write-FileContent "docker-compose.monitoring.yml" $composeMonitoringContent

$prometheusConfigContent = @'
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'catalog-service'
    static_configs:
      - targets: ['catalog-svc:5001']
'@
Write-FileContent "monitoring/prometheus.yml" $prometheusConfigContent

# DEPLOYMENT GUIDE
Write-Host "[8/8] Creating deployment guide..." -ForegroundColor Yellow

$deploymentGuideContent = @'
# Gearify Deployment Guide

## Prerequisites
- Docker and Docker Compose
- .NET 8 SDK
- Node.js 20+
- AWS CLI (for cloud deployment)

## Local Development

### Step 1: Bootstrap
```powershell
cd C:\Gearify
.\bootstrap-gearify-fixed.ps1
.\bootstrap-enhancements-fixed.ps1
.\bootstrap-complete-fixed.ps1
```

### Step 2: Start Services
```powershell
cd gearify-umbrella
docker-compose up --build -d
```

### Step 3: Verify
- Web: http://localhost:4200
- Gateway: http://localhost:8080
- Catalog: http://localhost:5001/swagger

## Cloud Deployment (AWS)

### Provision Infrastructure
```bash
cd gearify-infra-templates/terraform
terraform init
terraform apply
```

### Build and Push Images
```bash
docker build -t catalog-svc ./gearify-catalog-svc
docker tag catalog-svc:latest <ecr-url>/catalog-svc:latest
docker push <ecr-url>/catalog-svc:latest
```

### Deploy with Helm
```bash
helm install catalog-service ./helm/catalog-service
```

## Monitoring

Start monitoring stack:
```bash
docker-compose -f docker-compose.monitoring.yml up -d
```

Access dashboards:
- Grafana: http://localhost:3000 (admin/admin)
- Prometheus: http://localhost:9090

## Troubleshooting

### View logs
```bash
docker-compose logs -f catalog-svc
```

### Check health
```bash
curl http://localhost:5001/health
```
'@
Write-FileContent "docs/DEPLOYMENT.md" $deploymentGuideContent

Set-Location $BaseDir

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Final Components Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Added:" -ForegroundColor Cyan
Write-Host "  - Inventory service with stock reservations" -ForegroundColor White
Write-Host "  - Tenant service with feature flags" -ForegroundColor White
Write-Host "  - Checkout flow (3-step wizard)" -ForegroundColor White
Write-Host "  - Product detail page with reviews" -ForegroundColor White
Write-Host "  - Authentication service and guards" -ForegroundColor White
Write-Host "  - Admin dashboard" -ForegroundColor White
Write-Host "  - Monitoring stack (Prometheus + Grafana)" -ForegroundColor White
Write-Host "  - Deployment guide`n" -ForegroundColor White

Write-Host "Run all three scripts:" -ForegroundColor Yellow
Write-Host "  1. .\bootstrap-gearify-fixed.ps1" -ForegroundColor White
Write-Host "  2. .\bootstrap-enhancements-fixed.ps1" -ForegroundColor White
Write-Host "  3. .\bootstrap-complete-fixed.ps1`n" -ForegroundColor White
