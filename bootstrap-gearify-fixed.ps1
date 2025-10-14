# Gearify Platform Bootstrap Script (FIXED)
# Creates all 15+ repositories with complete file contents
# Run from: C:\Gearify\

param(
    [string]$BaseDir = "C:\Gearify"
)

$ErrorActionPreference = "Stop"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Gearify Platform Bootstrap" -ForegroundColor Cyan
Write-Host "Creating 15+ microservice repositories" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$repos = @(
    "gearify-umbrella",
    "gearify-api-gateway",
    "gearify-catalog-svc",
    "gearify-search-svc",
    "gearify-cart-svc",
    "gearify-order-svc",
    "gearify-payment-svc",
    "gearify-shipping-svc",
    "gearify-inventory-svc",
    "gearify-tenant-svc",
    "gearify-media-svc",
    "gearify-notification-svc",
    "gearify-web",
    "gearify-shared-kernel",
    "gearify-shared-contracts",
    "gearify-infra-templates"
)

# Create base directory
if (-not (Test-Path $BaseDir)) {
    New-Item -ItemType Directory -Path $BaseDir | Out-Null
}

Set-Location $BaseDir

# Helper function to create directories
function New-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

# Helper function to write files
function Write-FileContent {
    param(
        [string]$Path,
        [string]$Content
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) {
        New-Directory $dir
    }
    $Content | Out-File -FilePath $Path -Encoding UTF8 -Force
}

Write-Host "[1/16] Creating gearify-shared-kernel..." -ForegroundColor Yellow
New-Directory "gearify-shared-kernel"
Set-Location "gearify-shared-kernel"

$csprojContent = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Version>1.0.0</Version>
    <PackageId>Gearify.SharedKernel</PackageId>
    <Authors>Gearify Team</Authors>
    <Description>Shared kernel for Gearify microservices</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="OpenTelemetry" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.1" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.SharedKernel.csproj" $csprojContent

$readmeContent = @'
# Gearify.SharedKernel

Shared abstractions, DTOs, and middleware for all Gearify microservices.

## Features
- Result<T> pattern
- Outbox contracts
- Correlation middleware
- Idempotency filters
- Common Serilog/OpenTelemetry setup

## Usage
```bash
dotnet add package Gearify.SharedKernel
```
'@
Write-FileContent "README.md" $readmeContent

$resultContent = @'
namespace Gearify.SharedKernel.Abstractions;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}
'@
Write-FileContent "Abstractions/Result.cs" $resultContent

$outboxContent = @'
namespace Gearify.SharedKernel.Abstractions;

public interface IOutboxMessage
{
    string Id { get; }
    string EventType { get; }
    string Payload { get; }
    DateTime OccurredAt { get; }
}
'@
Write-FileContent "Abstractions/IOutboxMessage.cs" $outboxContent

$middlewareContent = @'
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Gearify.SharedKernel.Middleware;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public CorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
'@
Write-FileContent "Middleware/CorrelationMiddleware.cs" $middlewareContent

$dockerfileContent = @'
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet pack -c Release -o /out

FROM scratch
COPY --from=build /out /packages
'@
Write-FileContent "Dockerfile" $dockerfileContent

$ciContent = @'
name: CI
on: [push, pull_request]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build
      - run: dotnet pack -c Release
'@
Write-FileContent ".github/workflows/ci.yml" $ciContent

Set-Location $BaseDir

Write-Host "[2/16] Creating gearify-shared-contracts..." -ForegroundColor Yellow
New-Directory "gearify-shared-contracts"
Set-Location "gearify-shared-contracts"

$packageJsonContent = @'
{
  "name": "@gearify/shared-contracts",
  "version": "1.0.0",
  "description": "OpenAPI specs and generated TypeScript/C# clients",
  "main": "dist/index.js",
  "types": "dist/index.d.ts"
}
'@
Write-FileContent "package.json" $packageJsonContent

$openApiContent = @'
openapi: 3.0.3
info:
  title: Catalog Service API
  version: 1.0.0
paths:
  /api/products:
    get:
      summary: List products
      responses:
        '200':
          description: Success
'@
Write-FileContent "openapi/catalog.yaml" $openApiContent

Set-Location $BaseDir

Write-Host "[3/16] Creating gearify-api-gateway..." -ForegroundColor Yellow
New-Directory "gearify-api-gateway"
Set-Location "gearify-api-gateway"

$gatewayCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Yarp.ReverseProxy" Version="2.1.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
    <PackageReference Include="AspNetCoreRateLimit" Version="5.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.ApiGateway.csproj" $gatewayCsprojContent

$programContent = @'
using Serilog;
using Serilog.Formatting.Json;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new() { Endpoint = "*", Limit = 100, Period = "1m" }
    };
});
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
    });

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

app.UseIpRateLimiting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
'@
Write-FileContent "Program.cs" $programContent

$appsettingsContent = @'
{
  "Jwt": {
    "Authority": "http://localhost:9000"
  },
  "ReverseProxy": {
    "Routes": {
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/catalog/{**catch-all}" }
      }
    },
    "Clusters": {
      "catalog-cluster": {
        "Destinations": { "dest1": { "Address": "http://catalog-svc:5001" } }
      }
    }
  }
}
'@
Write-FileContent "appsettings.json" $appsettingsContent

$gatewayDockerContent = @'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Gearify.ApiGateway.dll"]
'@
Write-FileContent "Dockerfile" $gatewayDockerContent

Set-Location $BaseDir

Write-Host "[4/16] Creating gearify-catalog-svc..." -ForegroundColor Yellow
New-Directory "gearify-catalog-svc"
Set-Location "gearify-catalog-svc"

$catalogCsprojContent = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.300" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
    <PackageReference Include="Mapster" Version="7.4.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
'@
Write-FileContent "Gearify.CatalogService.csproj" $catalogCsprojContent

$catalogProgramContent = @'
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using MediatR;
using FluentValidation;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var dynamoConfig = new AmazonDynamoDBConfig
{
    ServiceURL = builder.Configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:8000"
};
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));
builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
'@
Write-FileContent "Program.cs" $catalogProgramContent

$productContent = @'
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.CatalogService.Domain;

[DynamoDBTable("gearify-products")]
public class Product
{
    [DynamoDBHashKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Brand { get; set; }
    public bool IsActive { get; set; } = true;
}
'@
Write-FileContent "Domain/Product.cs" $productContent

$catalogDockerContent = @'
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "Gearify.CatalogService.dll"]
'@
Write-FileContent "Dockerfile" $catalogDockerContent

Set-Location $BaseDir

# Create remaining services with minimal scaffolding
$serviceRepos = @(
    @{Name="gearify-search-svc"; Port=5002; Service="Search"},
    @{Name="gearify-cart-svc"; Port=5003; Service="Cart"},
    @{Name="gearify-order-svc"; Port=5004; Service="Order"},
    @{Name="gearify-payment-svc"; Port=5005; Service="Payment"},
    @{Name="gearify-shipping-svc"; Port=5006; Service="Shipping"},
    @{Name="gearify-inventory-svc"; Port=5007; Service="Inventory"},
    @{Name="gearify-tenant-svc"; Port=5008; Service="Tenant"},
    @{Name="gearify-media-svc"; Port=5009; Service="Media"},
    @{Name="gearify-notification-svc"; Port=5010; Service="Notification"}
)

$i = 5
foreach ($svc in $serviceRepos) {
    Write-Host "[$i/16] Creating $($svc.Name)..." -ForegroundColor Yellow
    $i++

    New-Directory $svc.Name
    Set-Location $svc.Name

    $projName = "Gearify.$($svc.Service)Service"

    $svcCsprojContent = @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
"@
    Write-FileContent "$projName.csproj" $svcCsprojContent

    $svcProgramContent = @'
using Serilog;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
builder.Host.UseSerilog();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
'@
    Write-FileContent "Program.cs" $svcProgramContent

    $svcDockerContent = @"
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE $($svc.Port)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:$($svc.Port)
ENTRYPOINT ["dotnet", "$projName.dll"]
"@
    Write-FileContent "Dockerfile" $svcDockerContent

    Set-Location $BaseDir
}

Write-Host "[14/16] Creating gearify-web..." -ForegroundColor Yellow
New-Directory "gearify-web"
Set-Location "gearify-web"

$webPackageJsonContent = @'
{
  "name": "gearify-web",
  "version": "1.0.0",
  "scripts": {
    "start": "ng serve",
    "build": "ng build"
  },
  "dependencies": {
    "@angular/core": "^18.0.0",
    "@angular/common": "^18.0.0",
    "@angular/router": "^18.0.0"
  }
}
'@
Write-FileContent "package.json" $webPackageJsonContent

$angularJsonContent = @'
{
  "version": 1,
  "projects": {
    "gearify-web": {
      "projectType": "application",
      "root": "",
      "sourceRoot": "src"
    }
  }
}
'@
Write-FileContent "angular.json" $angularJsonContent

$webDockerContent = @'
FROM node:20 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-slim
WORKDIR /app
COPY --from=build /app/dist ./dist
EXPOSE 4200
CMD ["node", "dist/server/main.js"]
'@
Write-FileContent "Dockerfile" $webDockerContent

Set-Location $BaseDir

Write-Host "[15/16] Creating gearify-infra-templates..." -ForegroundColor Yellow
New-Directory "gearify-infra-templates"
Set-Location "gearify-infra-templates"

$infraReadmeContent = @'
# Gearify Infrastructure Templates

Terraform modules, Helm charts, and Argo CD Applications.
'@
Write-FileContent "README.md" $infraReadmeContent

$terraformMainContent = @'
provider "aws" {
  region = var.region
}

resource "aws_dynamodb_table" "products" {
  name           = "gearify-products"
  billing_mode   = "PAY_PER_REQUEST"
  hash_key       = "Id"
  attribute {
    name = "Id"
    type = "S"
  }
}
'@
Write-FileContent "terraform/main.tf" $terraformMainContent

Set-Location $BaseDir

Write-Host "[16/16] Creating gearify-umbrella..." -ForegroundColor Yellow
New-Directory "gearify-umbrella"
Set-Location "gearify-umbrella"

$composeContent = @'
version: '3.8'
services:
  dynamodb:
    image: amazon/dynamodb-local
    ports: ['8000:8000']

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
    ports: ['5432:5432']

  redis:
    image: redis:7
    ports: ['6379:6379']

  catalog-svc:
    build:
      context: ../gearify-catalog-svc
    ports: ['5001:5001']
    environment:
      AWS__DynamoDB__ServiceURL: http://dynamodb:8000
    depends_on: [dynamodb]

  api-gateway:
    build:
      context: ../gearify-api-gateway
    ports: ['8080:8080']
    depends_on: [catalog-svc]

  web:
    build:
      context: ../gearify-web
    ports: ['4200:4200']
'@
Write-FileContent "docker-compose.yml" $composeContent

$makefileContent = @'
.PHONY: up down build

up:
	docker compose up --build -d

down:
	docker compose down

build:
	docker compose build
'@
Write-FileContent "Makefile" $makefileContent

$runbookContent = @'
# Gearify Runbook

## Quick Start
```bash
cd gearify-umbrella
make up
```

## Endpoints
- Web: http://localhost:4200
- Gateway: http://localhost:8080
- Catalog: http://localhost:5001/swagger
'@
Write-FileContent "docs/RUNBOOK.md" $runbookContent

Set-Location $BaseDir

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Bootstrap Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Created repositories:" -ForegroundColor Cyan
foreach ($repo in $repos) {
    Write-Host "  $repo" -ForegroundColor White
}

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. cd gearify-umbrella" -ForegroundColor White
Write-Host "2. make up" -ForegroundColor White
Write-Host "3. Open http://localhost:4200" -ForegroundColor White
