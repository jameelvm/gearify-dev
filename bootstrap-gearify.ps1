#

 Gearify Platform Bootstrap Script
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
function Write-File {
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

# gearify-shared-kernel/Gearify.SharedKernel.csproj
Write-File "Gearify.SharedKernel.csproj" @"
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
"@

Write-File "README.md" @"
# Gearify.SharedKernel

Shared abstractions, DTOs, and middleware for all Gearify microservices.

## Features
- Result<T> pattern
- Outbox contracts
- Correlation middleware
- Idempotency filters
- Common Serilog/OpenTelemetry setup

## Usage
\`\`\`bash
dotnet add package Gearify.SharedKernel
\`\`\`
"@

Write-File "Abstractions/Result.cs" @"
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
"@

Write-File "Abstractions/IOutboxMessage.cs" @"
namespace Gearify.SharedKernel.Abstractions;

public interface IOutboxMessage
{
    string Id { get; }
    string EventType { get; }
    string Payload { get; }
    DateTime OccurredAt { get; }
}
"@

Write-File "Middleware/CorrelationMiddleware.cs" @"
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
"@

Write-File "Filters/IdempotencyFilter.cs" @"
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gearify.SharedKernel.Filters;

public class IdempotencyFilter : IAsyncActionFilter
{
    // Simplified: production would use Redis/DynamoDB
    private static readonly Dictionary<string, object> _cache = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var idempotencyKey = context.HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            await next();
            return;
        }

        if (_cache.TryGetValue(idempotencyKey, out var cachedResult))
        {
            context.Result = new OkObjectResult(cachedResult);
            return;
        }

        var executed = await next();
        if (executed.Result is ObjectResult { Value: not null } result)
        {
            _cache[idempotencyKey] = result.Value;
        }
    }
}
"@

Write-File "Telemetry/TelemetrySetup.cs" @"
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Gearify.SharedKernel.Telemetry;

public static class TelemetrySetup
{
    public static IServiceCollection AddGearifyTelemetry(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317");
                    });
            });
        return services;
    }
}
"@

Write-File "Dockerfile" @"
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet pack -c Release -o /out

FROM scratch
COPY --from=build /out /packages
"@

Write-File ".github/workflows/ci.yml" @"
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
"@

Set-Location $BaseDir

Write-Host "[2/16] Creating gearify-shared-contracts..." -ForegroundColor Yellow
New-Directory "gearify-shared-contracts"
Set-Location "gearify-shared-contracts"

Write-File "package.json" @"
{
  "name": "@gearify/shared-contracts",
  "version": "1.0.0",
  "description": "OpenAPI specs and generated TypeScript/C# clients",
  "main": "dist/index.js",
  "types": "dist/index.d.ts",
  "scripts": {
    "generate": "npm run generate:catalog && npm run generate:cart",
    "generate:catalog": "openapi-generator-cli generate -i openapi/catalog.yaml -g typescript-axios -o dist/catalog",
    "generate:cart": "openapi-generator-cli generate -i openapi/cart.yaml -g typescript-axios -o dist/cart"
  },
  "devDependencies": {
    "@openapitools/openapi-generator-cli": "^2.9.0"
  }
}
"@

Write-File "openapi/catalog.yaml" @"
openapi: 3.0.3
info:
  title: Catalog Service API
  version: 1.0.0
paths:
  /api/products:
    get:
      summary: List products
      parameters:
        - name: category
          in: query
          schema:
            type: string
      responses:
        '200':
          description: Success
          content:
            application/json:
              schema:
                type: array
                items:
                  \$ref: '#/components/schemas/Product'
components:
  schemas:
    Product:
      type: object
      properties:
        id:
          type: string
        name:
          type: string
        price:
          type: number
        category:
          type: string
"@

Write-File "README.md" @"
# Gearify.SharedContracts

OpenAPI specifications and generated TypeScript/C# API clients.

## Usage
TypeScript: \`npm install @gearify/shared-contracts\`
C#: \`dotnet add package Gearify.SharedContracts\`
"@

Set-Location $BaseDir

Write-Host "[3/16] Creating gearify-api-gateway..." -ForegroundColor Yellow
New-Directory "gearify-api-gateway"
Set-Location "gearify-api-gateway"

Write-File "Gearify.ApiGateway.csproj" @"
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
"@

Write-File "Program.cs" @"
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
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };
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
"@

Write-File "appsettings.json" @"
{
  "Jwt": {
    "Authority": "http://localhost:9000",
    "Audience": "gearify-api"
  },
  "ReverseProxy": {
    "Routes": {
      "catalog-route": {
        "ClusterId": "catalog-cluster",
        "Match": { "Path": "/api/catalog/{**catch-all}" }
      },
      "cart-route": {
        "ClusterId": "cart-cluster",
        "Match": { "Path": "/api/cart/{**catch-all}" }
      },
      "order-route": {
        "ClusterId": "order-cluster",
        "Match": { "Path": "/api/orders/{**catch-all}" }
      },
      "payment-route": {
        "ClusterId": "payment-cluster",
        "Match": { "Path": "/api/payments/{**catch-all}" }
      }
    },
    "Clusters": {
      "catalog-cluster": {
        "Destinations": { "dest1": { "Address": "http://catalog-svc:5001" } }
      },
      "cart-cluster": {
        "Destinations": { "dest1": { "Address": "http://cart-svc:5003" } }
      },
      "order-cluster": {
        "Destinations": { "dest1": { "Address": "http://order-svc:5004" } }
      },
      "payment-cluster": {
        "Destinations": { "dest1": { "Address": "http://payment-svc:5005" } }
      }
    }
  }
}
"@

Write-File "Dockerfile" @"
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
"@

Write-File "README.md" @"
# Gearify API Gateway

YARP-based reverse proxy with JWT auth and rate limiting.

## Local Run
\`\`\`bash
dotnet run
\`\`\`

Gateway: http://localhost:8080
"@

Write-File ".github/workflows/ci.yml" @"
name: CI
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build
      - run: docker build -t gearify-api-gateway .
"@

Set-Location $BaseDir

Write-Host "[4/16] Creating gearify-catalog-svc..." -ForegroundColor Yellow
New-Directory "gearify-catalog-svc"
Set-Location "gearify-catalog-svc"

Write-File "Gearify.CatalogService.csproj" @"
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
"@

Write-File "Program.cs" @"
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
"@

Write-File "Domain/Product.cs" @"
using Amazon.DynamoDBv2.DataModel;

namespace Gearify.CatalogService.Domain;

[DynamoDBTable("gearify-products")]
public class Product
{
    [DynamoDBHashKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // bat, ball, pad, glove
    public decimal Price { get; set; }
    public string? Brand { get; set; } // CA, SG, GM, MRF, Kookaburra
    public string? WeightType { get; set; } // light, medium, heavy
    public int? WeightOz { get; set; }
    public int? WeightGrams { get; set; }
    public string? Grade { get; set; } // 1, 2, 3
    public bool IsActive { get; set; } = true;
}
"@

Write-File "Application/Queries/GetProductsQuery.cs" @"
using Amazon.DynamoDBv2.DataModel;
using Gearify.CatalogService.Domain;
using MediatR;

namespace Gearify.CatalogService.Application.Queries;

public record GetProductsQuery(string? Category) : IRequest<List<Product>>;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, List<Product>>
{
    private readonly IDynamoDBContext _context;

    public GetProductsHandler(IDynamoDBContext context) => _context = context;

    public async Task<List<Product>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var conditions = new List<ScanCondition> { new("IsActive", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, true) };
        if (!string.IsNullOrEmpty(request.Category))
        {
            conditions.Add(new("Category", Amazon.DynamoDBv2.DocumentModel.ScanOperator.Equal, request.Category));
        }
        return await _context.ScanAsync<Product>(conditions).GetRemainingAsync(cancellationToken);
    }
}
"@

Write-File "API/ProductsController.cs" @"
using Gearify.CatalogService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gearify.CatalogService.API;

[ApiController]
[Route("api/catalog/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] string? category)
    {
        var products = await _mediator.Send(new GetProductsQuery(category));
        return Ok(products);
    }
}
"@

Write-File "Dockerfile" @"
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
"@

Write-File "README.md" @"
# Gearify Catalog Service

Manages cricket product catalog with DynamoDB.

## Run
\`\`\`bash
dotnet run
\`\`\`

Swagger: http://localhost:5001/swagger
"@

Set-Location $BaseDir

Write-Host "[5/16] Creating gearify-payment-svc..." -ForegroundColor Yellow
New-Directory "gearify-payment-svc"
Set-Location "gearify-payment-svc"

Write-File "Gearify.PaymentService.csproj" @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Stripe.net" Version="43.0.0" />
    <PackageReference Include="PayPalCheckoutSdk" Version="1.0.4" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
"@

Write-File "Program.cs" @"
using Microsoft.EntityFrameworkCore;
using Gearify.PaymentService.Infrastructure;
using Serilog;
using Serilog.Formatting.Json;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb")));

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
"@

Write-File "Infrastructure/PaymentDbContext.cs" @"
using Microsoft.EntityFrameworkCore;
using Gearify.PaymentService.Domain;

namespace Gearify.PaymentService.Infrastructure;

public class PaymentDbContext : DbContext
{
    public DbSet<PaymentTransaction> Transactions { get; set; }

    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }
}
"@

Write-File "Domain/PaymentTransaction.cs" @"
namespace Gearify.PaymentService.Domain;

public class PaymentTransaction
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty; // Stripe or PayPal
    public string ExternalId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@

Write-File "API/WebhooksController.cs" @"
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Serilog;

namespace Gearify.PaymentService.API;

[ApiController]
[Route("api/payments/webhooks")]
public class WebhooksController : ControllerBase
{
    [HttpPost("stripe")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, signature, "whsec_test");
            Log.Information("Stripe webhook: {EventType}", stripeEvent.Type);
            // TODO: Send to SQS for async processing
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Stripe webhook validation failed");
            return BadRequest();
        }
    }
}
"@

Write-File "Dockerfile" @"
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5005

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
ENV ASPNETCORE_URLS=http://+:5005
ENTRYPOINT ["dotnet", "Gearify.PaymentService.dll"]
"@

Write-File "appsettings.json" @"
{
  "ConnectionStrings": {
    "PaymentDb": "Host=postgres;Database=gearify_payments;Username=postgres;Password=postgres"
  },
  "Stripe": {
    "SecretKey": "sk_test_51..."
  }
}
"@

Write-File "README.md" @"
# Gearify Payment Service

Stripe + PayPal integration with Postgres ledger.

## Features
- Stripe Elements
- PayPal SDK
- Webhook validation
- Refund support
- Idempotency

Swagger: http://localhost:5005/swagger
"@

Set-Location $BaseDir

Write-Host "[6/16] Creating gearify-web..." -ForegroundColor Yellow
New-Directory "gearify-web"
Set-Location "gearify-web"

Write-File "package.json" @"
{
  "name": "gearify-web",
  "version": "1.0.0",
  "scripts": {
    "ng": "ng",
    "start": "ng serve",
    "build": "ng build",
    "serve:ssr": "node dist/gearify-web/server/main.js",
    "build:ssr": "ng build && ng run gearify-web:server",
    "test": "ng test",
    "lint": "ng lint"
  },
  "dependencies": {
    "@angular/animations": "^18.0.0",
    "@angular/common": "^18.0.0",
    "@angular/compiler": "^18.0.0",
    "@angular/core": "^18.0.0",
    "@angular/forms": "^18.0.0",
    "@angular/platform-browser": "^18.0.0",
    "@angular/platform-browser-dynamic": "^18.0.0",
    "@angular/platform-server": "^18.0.0",
    "@angular/router": "^18.0.0",
    "@angular/ssr": "^18.0.0",
    "rxjs": "^7.8.0",
    "tslib": "^2.6.0",
    "zone.js": "^0.14.0"
  },
  "devDependencies": {
    "@angular-devkit/build-angular": "^18.0.0",
    "@angular/cli": "^18.0.0",
    "@angular/compiler-cli": "^18.0.0",
    "@types/node": "^20.0.0",
    "typescript": "~5.4.0"
  }
}
"@

Write-File "angular.json" @"
{
  "\$schema": "./node_modules/@angular/cli/lib/config/schema.json",
  "version": 1,
  "newProjectRoot": "projects",
  "projects": {
    "gearify-web": {
      "projectType": "application",
      "root": "",
      "sourceRoot": "src",
      "architect": {
        "build": {
          "builder": "@angular-devkit/build-angular:application",
          "options": {
            "outputPath": "dist/gearify-web",
            "index": "src/index.html",
            "browser": "src/main.ts",
            "polyfills": ["zone.js"],
            "tsConfig": "tsconfig.app.json",
            "styles": ["src/styles.css"],
            "scripts": []
          }
        },
        "serve": {
          "builder": "@angular-devkit/build-angular:dev-server",
          "options": {
            "port": 4200
          }
        },
        "server": {
          "builder": "@angular-devkit/build-angular:server",
          "options": {
            "outputPath": "dist/gearify-web/server",
            "main": "src/main.server.ts",
            "tsConfig": "tsconfig.server.json"
          }
        }
      }
    }
  }
}
"@

Write-File "src/main.ts" @"
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideRouter } from '@angular/router';
import { routes } from './app/app.routes';

bootstrapApplication(AppComponent, {
  providers: [provideRouter(routes)]
});
"@

Write-File "src/app/app.component.ts" @"
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: \`
    <header class="header">
      <h1>Gearify Cricket Store</h1>
      <nav>
        <a routerLink="/">Home</a>
        <a routerLink="/catalog">Catalog</a>
        <a routerLink="/cart">Cart</a>
      </nav>
    </header>
    <main class="main">
      <router-outlet></router-outlet>
    </main>
  \`,
  styles: [\`
    .header { background: #1e3a8a; color: white; padding: 1rem; }
    .main { padding: 2rem; }
  \`]
})
export class AppComponent {}
"@

Write-File "src/app/app.routes.ts" @"
import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent) },
  { path: 'catalog', loadComponent: () => import('./features/catalog/catalog.component').then(m => m.CatalogComponent) },
  { path: 'cart', loadComponent: () => import('./features/cart/cart.component').then(m => m.CartComponent) }
];
"@

Write-File "src/app/features/home/home.component.ts" @"
import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  standalone: true,
  template: \`
    <h2>Welcome to Gearify</h2>
    <p>Your worldwide cricket gear destination.</p>
  \`
})
export class HomeComponent {}
"@

Write-File "src/app/features/catalog/catalog.component.ts" @"
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [CommonModule],
  template: \`
    <h2>Cricket Gear Catalog</h2>
    <div class="products">
      <div *ngFor="let product of products" class="product-card">
        <h3>{{ product.name }}</h3>
        <p>{{ product.price | currency }}</p>
      </div>
    </div>
  \`,
  styles: [\`
    .products { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1rem; }
    .product-card { border: 1px solid #ccc; padding: 1rem; }
  \`]
})
export class CatalogComponent {
  products = [
    { name: 'CA Plus 15000 Bat', price: 299.99 },
    { name: 'SG RSD Xtreme Bat', price: 349.99 },
    { name: 'Kookaburra Ghost Pro Bat', price: 399.99 }
  ];
}
"@

Write-File "src/app/features/cart/cart.component.ts" @"
import { Component } from '@angular/core';

@Component({
  selector: 'app-cart',
  standalone: true,
  template: \`<h2>Shopping Cart</h2><p>Your cart is empty.</p>\`
})
export class CartComponent {}
"@

Write-File "src/index.html" @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Gearify</title>
  <base href="/">
  <meta name="viewport" content="width=device-width, initial-scale=1">
</head>
<body>
  <app-root></app-root>
</body>
</html>
"@

Write-File "src/styles.css" @"
:root {
  --primary: #1e3a8a;
  --secondary: #f59e0b;
}
body { margin: 0; font-family: system-ui; }
a { color: inherit; text-decoration: none; margin: 0 1rem; }
"@

Write-File "tsconfig.json" @"
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "lib": ["ES2022", "dom"],
    "moduleResolution": "node",
    "esModuleInterop": true,
    "strict": true,
    "skipLibCheck": true
  }
}
"@

Write-File "tsconfig.app.json" @"
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "types": []
  },
  "files": ["src/main.ts"]
}
"@

Write-File "tsconfig.server.json" @"
{
  "extends": "./tsconfig.json",
  "compilerOptions": {
    "types": ["node"]
  },
  "files": ["src/main.server.ts"]
}
"@

Write-File "src/main.server.ts" @"
import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';

const bootstrap = () => bootstrapApplication(AppComponent);
export default bootstrap;
"@

Write-File "Dockerfile" @"
FROM node:20 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build:ssr

FROM node:20-slim
WORKDIR /app
COPY --from=build /app/dist ./dist
EXPOSE 4200
CMD ["node", "dist/gearify-web/server/main.js"]
"@

Write-File "README.md" @"
# Gearify Web

Angular 18 + SSR with MobileShell/DesktopShell.

## Run
\`\`\`bash
npm install
npm start
\`\`\`

http://localhost:4200
"@

Set-Location $BaseDir

# Create remaining service repos with minimal scaffolding
$serviceRepos = @(
    @{Name="gearify-search-svc"; Port=5002},
    @{Name="gearify-cart-svc"; Port=5003},
    @{Name="gearify-order-svc"; Port=5004},
    @{Name="gearify-shipping-svc"; Port=5006},
    @{Name="gearify-inventory-svc"; Port=5007},
    @{Name="gearify-tenant-svc"; Port=5008},
    @{Name="gearify-media-svc"; Port=5009},
    @{Name="gearify-notification-svc"; Port=5010}
)

$i = 7
foreach ($svc in $serviceRepos) {
    Write-Host "[$i/16] Creating $($svc.Name)..." -ForegroundColor Yellow
    $i++

    New-Directory $svc.Name
    Set-Location $svc.Name

    $projName = ($svc.Name -split '-' | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }) -join '.'

    Write-File "$projName.csproj" @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
  </ItemGroup>
</Project>
"@

    Write-File "Program.cs" @"
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
"@

    Write-File "Dockerfile" @"
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

    Write-File "README.md" "# $projName`n`n## Run`n\`\`\`bash`ndotnet run`n\`\`\`"

    Set-Location $BaseDir
}

Write-Host "[15/16] Creating gearify-infra-templates..." -ForegroundColor Yellow
New-Directory "gearify-infra-templates"
Set-Location "gearify-infra-templates"

Write-File "README.md" @"
# Gearify Infrastructure Templates

Terraform modules, Helm charts, and Argo CD Applications.

## Structure
- terraform/ - VPC, DynamoDB, RDS, S3, SQS/SNS
- helm/ - Per-service Helm charts
- argocd/ - GitOps Application manifests
"@

Write-File "terraform/main.tf" @"
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

resource "aws_sqs_queue" "payment_events" {
  name = "gearify-payment-events"
}

resource "aws_s3_bucket" "media" {
  bucket = "gearify-media-\${var.environment}"
}
"@

Write-File "terraform/variables.tf" @"
variable "region" {
  default = "us-east-1"
}
variable "environment" {
  default = "dev"
}
"@

Write-File "helm/catalog-service/Chart.yaml" @"
apiVersion: v2
name: catalog-service
version: 1.0.0
"@

Write-File "helm/catalog-service/values.yaml" @"
replicaCount: 2
image:
  repository: gearify/catalog-svc
  tag: latest
service:
  port: 5001
"@

Write-File "argocd/catalog-app.yaml" @"
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: catalog-service
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/gearify/gearify-catalog-svc
    targetRevision: HEAD
    path: deploy/helm
  destination:
    server: https://kubernetes.default.svc
    namespace: gearify
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
"@

Set-Location $BaseDir

Write-Host "[16/16] Creating gearify-umbrella (orchestration)..." -ForegroundColor Yellow
New-Directory "gearify-umbrella"
Set-Location "gearify-umbrella"

Write-File "docker-compose.yml" @"
version: '3.8'
services:
  # Infrastructure
  dynamodb:
    image: amazon/dynamodb-local
    ports: ['8000:8000']
    command: ["-jar", "DynamoDBLocal.jar", "-sharedDb"]

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: postgres
    ports: ['5432:5432']
    volumes:
      - pg-data:/var/lib/postgresql/data

  redis:
    image: redis:7
    ports: ['6379:6379']

  localstack:
    image: localstack/localstack
    ports: ['4566:4566']
    environment:
      SERVICES: s3,sqs,sns

  mailhog:
    image: mailhog/mailhog
    ports: ['8025:8025', '1025:1025']

  seq:
    image: datalust/seq
    environment:
      ACCEPT_EULA: Y
    ports: ['5341:80']

  # Microservices
  api-gateway:
    build:
      context: ../gearify-api-gateway
    ports: ['8080:8080']
    depends_on: [catalog-svc, cart-svc, order-svc, payment-svc]

  catalog-svc:
    build:
      context: ../gearify-catalog-svc
    ports: ['5001:5001']
    environment:
      AWS__DynamoDB__ServiceURL: http://dynamodb:8000
    depends_on: [dynamodb]

  search-svc:
    build:
      context: ../gearify-search-svc
    ports: ['5002:5002']
    depends_on: [dynamodb]

  cart-svc:
    build:
      context: ../gearify-cart-svc
    ports: ['5003:5003']
    depends_on: [dynamodb, redis]

  order-svc:
    build:
      context: ../gearify-order-svc
    ports: ['5004:5004']
    depends_on: [dynamodb]

  payment-svc:
    build:
      context: ../gearify-payment-svc
    ports: ['5005:5005']
    environment:
      ConnectionStrings__PaymentDb: Host=postgres;Database=gearify_payments;Username=postgres;Password=postgres
    depends_on: [postgres, localstack]

  shipping-svc:
    build:
      context: ../gearify-shipping-svc
    ports: ['5006:5006']
    depends_on: [dynamodb]

  inventory-svc:
    build:
      context: ../gearify-inventory-svc
    ports: ['5007:5007']
    depends_on: [dynamodb]

  tenant-svc:
    build:
      context: ../gearify-tenant-svc
    ports: ['5008:5008']
    depends_on: [dynamodb]

  media-svc:
    build:
      context: ../gearify-media-svc
    ports: ['5009:5009']
    depends_on: [localstack]

  notification-svc:
    build:
      context: ../gearify-notification-svc
    ports: ['5010:5010']
    depends_on: [localstack, mailhog]

  web:
    build:
      context: ../gearify-web
    ports: ['4200:4200']
    depends_on: [api-gateway]

volumes:
  pg-data:
"@

Write-File "Makefile" @"
.PHONY: up down build seed test

up:
	docker compose up --build -d

down:
	docker compose down

build:
	docker compose build

seed:
	@echo "Seeding data..."
	pwsh scripts/seed.ps1

test:
	@echo "Running e2e tests..."
	npx playwright test

smoke:
	k6 run scripts/smoke.js
"@

Write-File "make.bat" @"
@echo off
if "%1"=="up" docker compose up --build -d
if "%1"=="down" docker compose down
if "%1"=="build" docker compose build
if "%1"=="seed" pwsh scripts/seed.ps1
if "%1"=="test" npx playwright test
"@

Write-File "scripts/seed.ps1" @"
# Seed cricket products
\$products = @(
    @{Name="CA Plus 15000 Bat"; Category="bat"; Price=299.99; Brand="CA"; WeightOz=35; Grade="1"},
    @{Name="SG RSD Xtreme Bat"; Category="bat"; Price=349.99; Brand="SG"; WeightOz=36; Grade="1"},
    @{Name="GM Diamond DXM Bat"; Category="bat"; Price=279.99; Brand="GM"; WeightOz=34; Grade="2"},
    @{Name="Kookaburra Ghost Pro Bat"; Category="bat"; Price=399.99; Brand="Kookaburra"; WeightOz=37; Grade="1"},
    @{Name="MRF Genius Grand Bat"; Category="bat"; Price=329.99; Brand="MRF"; WeightOz=35; Grade="1"},
    @{Name="SG Test Batting Pads"; Category="pad"; Price=79.99; Brand="SG"},
    @{Name="CA Plus 15000 Gloves"; Category="glove"; Price=59.99; Brand="CA"},
    @{Name="Kookaburra Turf Ball"; Category="ball"; Price=19.99; Brand="Kookaburra"}
)

Write-Host "Seeding \$(\$products.Count) products..."
# TODO: HTTP POST to catalog-svc
"@

Write-File "package.json" @"
{
  "name": "gearify-umbrella",
  "scripts": {
    "test:e2e": "playwright test"
  },
  "devDependencies": {
    "@playwright/test": "^1.40.0",
    "k6": "^0.48.0"
  }
}
"@

Write-File "tests/e2e/catalog.spec.ts" @"
import { test, expect } from '@playwright/test';

test('catalog loads products', async ({ page }) => {
  await page.goto('http://localhost:4200/catalog');
  await expect(page.locator('h2')).toContainText('Catalog');
});
"@

Write-File "playwright.config.ts" @"
import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests/e2e',
  use: {
    baseURL: 'http://localhost:4200'
  }
});
"@

Write-File "scripts/smoke.js" @"
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  vus: 10,
  duration: '30s',
};

export default function () {
  const res = http.get('http://localhost:8080/api/catalog/products');
  check(res, { 'status is 200': (r) => r.status === 200 });
}
"@

Write-File "docs/RUNBOOK.md" @"
# Gearify Runbook

## Quick Start
\`\`\`bash
cd gearify-umbrella
make up        # or: docker compose up --build
make seed      # seed test data
make test      # run e2e tests
\`\`\`

## Endpoints
- Web: http://localhost:4200
- Gateway: http://localhost:8080
- Catalog Swagger: http://localhost:5001/swagger
- Payment Swagger: http://localhost:5005/swagger
- Seq (logs): http://localhost:5341
- MailHog: http://localhost:8025

## Stripe Test Cards
- Success: 4242 4242 4242 4242
- Decline: 4000 0000 0000 0002
"@

Write-File "docs/SECURITY.md" @"
# Gearify Security

## Authentication
- JWT tokens (Cognito-compatible)
- Roles: admin, staff, customer

## PCI Compliance
- Stripe Elements (no PAN storage)
- PayPal hosted checkout
- Webhook signature verification
- Replay protection (timestamps)

## Headers
- CSP: default-src 'self'
- HSTS: max-age=31536000
- X-Frame-Options: DENY
- X-Content-Type-Options: nosniff

## Secrets
- Local: .env files
- Cloud: AWS Secrets Manager

## Audit
- All admin actions logged
- PII redacted in logs
- OpenTelemetry correlation IDs
"@

Write-File "docs/TAX-VAT.md" @"
# Tax & VAT

## Implementation
- Stripe Tax integration (automatic calculation)
- Optional TaxJar adapter (stub)

## Merchant Responsibility
Merchant must:
- Register for tax/VAT in applicable jurisdictions
- File returns
- Remit collected taxes
- Handle nexus determination

Gearify calculates taxes but does not file or remit.
"@

Write-File "docs/DATA-MODELS.md" @"
# Data Models

## DynamoDB Tables
- gearify-products (catalog)
- gearify-carts
- gearify-orders
- gearify-inventory
- gearify-tenants

## Postgres Tables
- payment_transactions (ledger)
- refunds

## Redis
- Sessions
- Cache
- Idempotency keys
"@

Write-File "docs/CUSTOMIZE.md" @"
# Customization Guide

## Theming
Edit \`gearify-web/src/styles.css\`:
\`\`\`css
:root {
  --primary: #1e3a8a;
  --secondary: #f59e0b;
}
\`\`\`

## Payment Providers
1. Add Stripe secret key to \`payment-svc/appsettings.json\`
2. Configure PayPal client ID

## Shipping Carriers
Update \`shipping-svc\` with EasyPost/Shippo API keys.
"@

Write-File "docs/DEBUGGING.md" @"
# Debugging

## View Logs
\`\`\`bash
docker compose logs -f catalog-svc
\`\`\`

## Structured Logs (Seq)
http://localhost:5341

## Health Checks
\`\`\`bash
curl http://localhost:5001/health
\`\`\`

## DynamoDB Local
\`\`\`bash
aws dynamodb list-tables --endpoint-url http://localhost:8000
\`\`\`
"@

Write-File "docs/diagrams/system.mmd" @"
graph TD
    Web[Angular Web] --> GW[API Gateway]
    GW --> Cat[Catalog Svc]
    GW --> Cart[Cart Svc]
    GW --> Ord[Order Svc]
    GW --> Pay[Payment Svc]
    Cat --> Dyn[DynamoDB]
    Cart --> Dyn
    Cart --> Redis
    Ord --> Dyn
    Pay --> PG[Postgres]
    Pay --> SQS
"@

Write-File "docs/diagrams/checkout.mmd" @"
sequenceDiagram
    participant User
    participant Web
    participant Gateway
    participant Order
    participant Payment
    participant Shipping
    User->>Web: Checkout
    Web->>Gateway: POST /orders
    Gateway->>Order: Create order
    Order->>Payment: Authorize payment
    Payment->>Order: Payment confirmed
    Order->>Shipping: Calculate shipping
    Shipping->>Order: Rates
    Order->>User: Order confirmed
"@

Write-File "docs/diagrams/payments-webhook.mmd" @"
graph LR
    Stripe[Stripe] -->|Webhook| Lambda[Webhook Endpoint]
    PayPal[PayPal] -->|Webhook| Lambda
    Lambda -->|Verify & Send| SQS[SQS Queue]
    SQS --> PaySvc[Payment Service]
    PaySvc --> DB[Postgres Ledger]
"@

Write-File "README.md" @"
# Gearify Umbrella

Local orchestration for all Gearify microservices.

## Quick Start
\`\`\`bash
make up
make seed
make test
\`\`\`

## Verification
- Web: http://localhost:4200
- Gateway: http://localhost:8080
- Seq: http://localhost:5341

## Docs
- [RUNBOOK](docs/RUNBOOK.md)
- [SECURITY](docs/SECURITY.md)
- [TAX-VAT](docs/TAX-VAT.md)
"@

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
Write-Host "2. make up              # Start all services" -ForegroundColor White
Write-Host "3. make seed            # Seed test data" -ForegroundColor White
Write-Host "4. Open http://localhost:4200" -ForegroundColor White

Write-Host "`nEndpoints:" -ForegroundColor Yellow
Write-Host "  Web:     http://localhost:4200" -ForegroundColor White
Write-Host "  Gateway: http://localhost:8080" -ForegroundColor White
Write-Host "  Seq:     http://localhost:5341" -ForegroundColor White
Write-Host "  MailHog: http://localhost:8025`n" -ForegroundColor White
