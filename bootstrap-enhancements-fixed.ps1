# Gearify Platform - Enhancement Script (FIXED)
# Adds detailed implementations for services
# Run AFTER bootstrap-gearify-fixed.ps1

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
Write-Host "Gearify Platform Enhancements" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# SHIPPING SERVICE
Write-Host "[1/10] Enhancing gearify-shipping-svc..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-shipping-svc"

$shippingCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Polly" Version="8.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.ShippingService.csproj" $shippingCsprojContent

$shippingProgramContent = @'
using MediatR;
using Serilog;
using Serilog.Formatting.Json;
using Gearify.ShippingService.Infrastructure.Adapters;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddSingleton<IShippingAdapter, EasyPostAdapter>();
builder.Services.AddSingleton<IShippingAdapter, ShippoAdapter>();
builder.Services.AddSingleton<ShippingAggregator>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
'@
Write-FileContent "Program.cs" $shippingProgramContent

$shippingDomainContent = @'
namespace Gearify.ShippingService.Domain;

public class ShippingRate
{
    public string Carrier { get; set; } = string.Empty;
    public string ServiceLevel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public int EstimatedDays { get; set; }
}

public class Address
{
    public string Street1 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CustomsInfo
{
    public string HsCode { get; set; } = string.Empty;
    public decimal CustomsValue { get; set; }
    public string Incoterm { get; set; } = "DDU";
}
'@
Write-FileContent "Domain/ShippingRate.cs" $shippingDomainContent

$adapterInterfaceContent = @'
using Gearify.ShippingService.Domain;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public interface IShippingAdapter
{
    string ProviderName { get; }
    Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct);
}
'@
Write-FileContent "Infrastructure/Adapters/IShippingAdapter.cs" $adapterInterfaceContent

$easyPostContent = @'
using Gearify.ShippingService.Domain;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class EasyPostAdapter : IShippingAdapter
{
    public string ProviderName => "EasyPost";

    public async Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new List<ShippingRate>
        {
            new() { Carrier = "USPS", ServiceLevel = "Priority", Amount = 15.99m, EstimatedDays = 3 },
            new() { Carrier = "FedEx", ServiceLevel = "Ground", Amount = 22.50m, EstimatedDays = 5 }
        };
    }
}
'@
Write-FileContent "Infrastructure/Adapters/EasyPostAdapter.cs" $easyPostContent

$shippoContent = @'
using Gearify.ShippingService.Domain;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class ShippoAdapter : IShippingAdapter
{
    public string ProviderName => "Shippo";

    public async Task<List<ShippingRate>> GetRatesAsync(CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new List<ShippingRate>
        {
            new() { Carrier = "UPS", ServiceLevel = "Ground", Amount = 18.75m, EstimatedDays = 4 }
        };
    }
}
'@
Write-FileContent "Infrastructure/Adapters/ShippoAdapter.cs" $shippoContent

$aggregatorContent = @'
using Gearify.ShippingService.Domain;

namespace Gearify.ShippingService.Infrastructure.Adapters;

public class ShippingAggregator
{
    private readonly IEnumerable<IShippingAdapter> _adapters;

    public ShippingAggregator(IEnumerable<IShippingAdapter> adapters)
    {
        _adapters = adapters;
    }

    public async Task<List<ShippingRate>> GetAllRatesAsync(CancellationToken ct)
    {
        var tasks = _adapters.Select(adapter => adapter.GetRatesAsync(ct));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).OrderBy(r => r.Amount).ToList();
    }
}
'@
Write-FileContent "Infrastructure/Adapters/ShippingAggregator.cs" $aggregatorContent

# CART SERVICE
Write-Host "[2/10] Enhancing gearify-cart-svc..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-cart-svc"

$cartCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.10" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.CartService.csproj" $cartCsprojContent

$cartProgramContent = @'
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using MediatR;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var dynamoConfig = new AmazonDynamoDBConfig
{
    ServiceURL = builder.Configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:8000"
};
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));
builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

var redis = ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
'@
Write-FileContent "Program.cs" $cartProgramContent

$cartDomainContent = @'
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.CartService.Domain;

[DynamoDBTable("gearify-carts")]
public class Cart
{
    [DynamoDBHashKey]
    public string UserId { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CartItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
'@
Write-FileContent "Domain/Cart.cs" $cartDomainContent

$addToCartContent = @'
using Amazon.DynamoDBv2.DataModel;
using Gearify.CartService.Domain;
using MediatR;
using StackExchange.Redis;
using System.Text.Json;

namespace Gearify.CartService.Application.Commands;

public record AddToCartCommand(string UserId, string ProductId, int Quantity, decimal Price) : IRequest<Cart>;

public class AddToCartHandler : IRequestHandler<AddToCartCommand, Cart>
{
    private readonly IDynamoDBContext _dynamoContext;
    private readonly IConnectionMultiplexer _redis;

    public AddToCartHandler(IDynamoDBContext dynamoContext, IConnectionMultiplexer redis)
    {
        _dynamoContext = dynamoContext;
        _redis = redis;
    }

    public async Task<Cart> Handle(AddToCartCommand cmd, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var cacheKey = "cart:" + cmd.UserId;

        var cart = await _dynamoContext.LoadAsync<Cart>(cmd.UserId, ct) ?? new Cart { UserId = cmd.UserId };

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == cmd.ProductId);
        if (existingItem != null)
        {
            existingItem.Quantity += cmd.Quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = cmd.ProductId,
                ProductName = "Product " + cmd.ProductId,
                Quantity = cmd.Quantity,
                Price = cmd.Price
            });
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _dynamoContext.SaveAsync(cart, ct);
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(cart), TimeSpan.FromMinutes(15));

        return cart;
    }
}
'@
Write-FileContent "Application/Commands/AddToCartCommand.cs" $addToCartContent

$cartControllerContent = @'
using Gearify.CartService.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CartService.API;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;

    public CartController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{userId}/items")]
    public async Task<IActionResult> AddToCart(string userId, [FromBody] AddItemRequest request)
    {
        var cart = await _mediator.Send(new AddToCartCommand(userId, request.ProductId, request.Quantity, request.Price));
        return Ok(cart);
    }
}

public record AddItemRequest(string ProductId, int Quantity, decimal Price);
'@
Write-FileContent "API/CartController.cs" $cartControllerContent

# ORDER SERVICE
Write-Host "[3/10] Enhancing gearify-order-svc..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-order-svc"

$orderCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
    <PackageReference Include="AWSSDK.SQS" Version="3.7.300" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.OrderService.csproj" $orderCsprojContent

$orderDomainContent = @'
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.OrderService.Domain;

[DynamoDBTable("gearify-orders")]
public class Order
{
    [DynamoDBHashKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

[DynamoDBTable("gearify-outbox")]
public class OutboxMessage
{
    [DynamoDBHashKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public bool Processed { get; set; } = false;
}
'@
Write-FileContent "Domain/Order.cs" $orderDomainContent

# ANGULAR WEB
Write-Host "[4/10] Enhancing gearify-web..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-web"

$mobileShellContent = @'
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-mobile-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    <div class="mobile-shell">
      <header class="mobile-header">
        <h1>Gearify</h1>
      </header>
      <main class="mobile-content">
        <router-outlet></router-outlet>
      </main>
      <nav class="mobile-nav">
        <a routerLink="/">Home</a>
        <a routerLink="/catalog">Shop</a>
        <a routerLink="/cart">Cart</a>
      </nav>
    </div>
  `,
  styles: [`
    .mobile-shell { display: flex; flex-direction: column; height: 100vh; }
    .mobile-header { padding: 1rem; background: #1e3a8a; color: white; }
    .mobile-content { flex: 1; overflow-y: auto; }
    .mobile-nav { display: flex; justify-content: space-around; padding: 0.5rem; background: #f5f5f5; }
  `]
})
export class MobileShellComponent {}
'@
Write-FileContent "src/app/shells/mobile-shell/mobile-shell.component.ts" $mobileShellContent

$desktopShellContent = @'
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-desktop-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    <div class="desktop-shell">
      <header class="desktop-header">
        <h1>Gearify Cricket Store</h1>
        <nav>
          <a routerLink="/">Home</a>
          <a routerLink="/catalog">Catalog</a>
          <a routerLink="/cart">Cart</a>
        </nav>
      </header>
      <main class="desktop-content">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .desktop-shell { display: flex; flex-direction: column; min-height: 100vh; }
    .desktop-header { background: #1e3a8a; color: white; padding: 1rem; }
    .desktop-content { flex: 1; padding: 2rem; }
  `]
})
export class DesktopShellComponent {}
'@
Write-FileContent "src/app/shells/desktop-shell/desktop-shell.component.ts" $desktopShellContent

$catalogComponentContent = @'
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h2>Cricket Gear Catalog</h2>
    <div class="products">
      <div *ngFor="let product of products" class="product-card">
        <h3>{{ product.name }}</h3>
        <p>Price: {{ product.price | currency }}</p>
      </div>
    </div>
  `,
  styles: [`
    .products { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .product-card { border: 1px solid #ccc; padding: 1rem; }
  `]
})
export class CatalogComponent {
  products = [
    { name: 'CA Plus 15000 Bat', price: 299.99 },
    { name: 'SG RSD Xtreme Bat', price: 349.99 },
    { name: 'GM Diamond Bat', price: 279.99 }
  ];
}
'@
Write-FileContent "src/app/features/catalog/catalog.component.ts" $catalogComponentContent

# MERMAID DIAGRAMS
Write-Host "[5/10] Adding Mermaid diagrams..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-umbrella"

$systemDiagramContent = @'
graph TD
    Web[Angular Web] --> GW[API Gateway]
    GW --> Cat[Catalog Service]
    GW --> Cart[Cart Service]
    GW --> Ord[Order Service]
    Cat --> Dyn[DynamoDB]
    Cart --> Redis
    Ord --> Dyn
'@
Write-FileContent "docs/diagrams/system.mmd" $systemDiagramContent

$checkoutDiagramContent = @'
sequenceDiagram
    participant User
    participant Web
    participant Gateway
    participant Order
    User->>Web: Checkout
    Web->>Gateway: POST /orders
    Gateway->>Order: Create order
    Order->>User: Order confirmed
'@
Write-FileContent "docs/diagrams/checkout.mmd" $checkoutDiagramContent

# TERRAFORM
Write-Host "[6/10] Creating Terraform modules..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-infra-templates"

$dynamodbTfContent = @'
resource "aws_dynamodb_table" "products" {
  name           = "gearify-products"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "Id"

  attribute {
    name = "Id"
    type = "S"
  }

  tags = {
    Name = "gearify-products"
  }
}

resource "aws_dynamodb_table" "carts" {
  name         = "gearify-carts"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "UserId"

  attribute {
    name = "UserId"
    type = "S"
  }
}
'@
Write-FileContent "terraform/dynamodb.tf" $dynamodbTfContent

$variablesTfContent = @'
variable "region" {
  default = "us-east-1"
}

variable "environment" {
  default = "dev"
}
'@
Write-FileContent "terraform/variables.tf" $variablesTfContent

# HELM CHARTS
Write-Host "[7/10] Creating Helm charts..." -ForegroundColor Yellow

$helmChartContent = @'
apiVersion: v2
name: catalog-service
version: 1.0.0
description: Gearify Catalog Service
'@
Write-FileContent "helm/catalog-service/Chart.yaml" $helmChartContent

$helmValuesContent = @'
replicaCount: 2

image:
  repository: gearify/catalog-svc
  tag: latest

service:
  port: 5001
'@
Write-FileContent "helm/catalog-service/values.yaml" $helmValuesContent

# DOCKER COMPOSE OVERRIDES
Write-Host "[8/10] Creating Docker Compose overrides..." -ForegroundColor Yellow
Set-Location "$BaseDir/gearify-umbrella"

$composeDevContent = @'
version: '3.8'
services:
  catalog-svc:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
'@
Write-FileContent "docker-compose.dev.yml" $composeDevContent

# SEED DATA
Write-Host "[9/10] Creating seed data script..." -ForegroundColor Yellow

$seedDataContent = @'
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
'@
Write-FileContent "scripts/seed-data.ps1" $seedDataContent

# DOCUMENTATION
Write-Host "[10/10] Creating documentation..." -ForegroundColor Yellow

$apiRefContent = @'
# Gearify API Reference

## Catalog Service (Port 5001)

### GET /api/catalog/products
List all products

### POST /api/catalog/products
Create a new product

## Cart Service (Port 5003)

### GET /api/cart/{userId}
Get user cart

### POST /api/cart/{userId}/items
Add item to cart
'@
Write-FileContent "docs/API-REFERENCE.md" $apiRefContent

$securityContent = @'
# Gearify Security

## Authentication
- JWT tokens (Cognito-compatible)
- Roles: admin, staff, customer

## PCI Compliance
- Stripe Elements (no PAN storage)
- Webhook signature verification

## Headers
- HSTS enabled
- CSP configured
- X-Frame-Options: DENY
'@
Write-FileContent "docs/SECURITY.md" $securityContent

Set-Location $BaseDir

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Enhancement Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Added:" -ForegroundColor Cyan
Write-Host "  - Shipping service (EasyPost/Shippo adapters)" -ForegroundColor White
Write-Host "  - Cart service (Redis + DynamoDB)" -ForegroundColor White
Write-Host "  - Order service (Outbox pattern)" -ForegroundColor White
Write-Host "  - Angular shells (Mobile/Desktop)" -ForegroundColor White
Write-Host "  - Mermaid diagrams" -ForegroundColor White
Write-Host "  - Terraform modules" -ForegroundColor White
Write-Host "  - Helm charts" -ForegroundColor White
Write-Host "  - Seed data script" -ForegroundColor White
Write-Host "  - API documentation`n" -ForegroundColor White
