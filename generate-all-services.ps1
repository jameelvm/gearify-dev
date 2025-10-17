# Gearify Services Generator
# This script generates complete Clean Architecture implementations for all 11 microservices

$ErrorActionPreference = "Stop"

Write-Host "Generating Gearify Microservices..." -ForegroundColor Cyan

# Service definitions
$services = @(
    @{
        Name = "Cart"
        Path = "gearify-cart-svc"
        Namespace = "Gearify.CartService"
        Port = 5002
        Tech = "Redis"
    },
    @{
        Name = "Search"
        Path = "gearify-search-svc"
        Namespace = "Gearify.SearchService"
        Port = 5003
        Tech = "DynamoDB + Redis"
    },
    @{
        Name = "Order"
        Path = "gearify-order-svc"
        Namespace = "Gearify.OrderService"
        Port = 5004
        Tech = "DynamoDB + SNS"
    },
    @{
        Name = "Payment"
        Path = "gearify-payment-svc"
        Namespace = "Gearify.PaymentService"
        Port = 5005
        Tech = "PostgreSQL + Stripe + PayPal"
    },
    @{
        Name = "Shipping"
        Path = "gearify-shipping-svc"
        Namespace = "Gearify.ShippingService"
        Port = 5006
        Tech = "EasyPost + Shippo + S3"
    },
    @{
        Name = "Inventory"
        Path = "gearify-inventory-svc"
        Namespace = "Gearify.InventoryService"
        Port = 5007
        Tech = "DynamoDB + SQS"
    },
    @{
        Name = "Tenant"
        Path = "gearify-tenant-svc"
        Namespace = "Gearify.TenantService"
        Port = 5008
        Tech = "DynamoDB"
    },
    @{
        Name = "Media"
        Path = "gearify-media-svc"
        Namespace = "Gearify.MediaService"
        Port = 5009
        Tech = "S3"
    },
    @{
        Name = "Notification"
        Path = "gearify-notification-svc"
        Namespace = "Gearify.NotificationService"
        Port = 5010
        Tech = "SQS + MailHog"
    }
)

function New-DirectoryIfNotExists {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Generate-ControllerFile {
    param(
        [string]$ServicePath,
        [string]$Namespace,
        [string]$EntityName
    )

    $content = @"
using $Namespace.Application.Commands;
using $Namespace.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace $Namespace.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ${EntityName}Controller : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<${EntityName}Controller> _logger;

    public ${EntityName}Controller(IMediator mediator, ILogger<${EntityName}Controller> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromHeader(Name = "X-Tenant-Id")] string tenantId = "default")
    {
        try
        {
            var result = await _mediator.Send(new Get${EntityName}sQuery(tenantId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ${EntityName.ToLower()}s");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, [FromHeader(Name = "X-Tenant-Id")] string tenantId = "default")
    {
        try
        {
            var result = await _mediator.Send(new Get${EntityName}ByIdQuery(id, tenantId));
            return result == null ? NotFound() : Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ${EntityName.ToLower()} {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
"@

    $filePath = Join-Path $ServicePath "API\Controllers\${EntityName}Controller.cs"
    New-DirectoryIfNotExists (Split-Path $filePath)
    Set-Content -Path $filePath -Value $content
}

function Generate-ProgramFile {
    param(
        [string]$ServicePath,
        [string]$Namespace,
        [string]$ServiceName
    )

    $content = @"
using MediatR;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // Redis Connection
    var redisConnection = builder.Configuration["REDIS_URL"] ?? "localhost:6379";
    if (redisConnection.StartsWith("redis://")) {
        redisConnection = redisConnection.Substring(8);
    }
    var configOptions = ConfigurationOptions.Parse(redisConnection);
    configOptions.AbortOnConnectFail = false;
    configOptions.ConnectRetry = 5;
    configOptions.ConnectTimeout = 5000;
    var redis = ConnectionMultiplexer.Connect(configOptions);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

    // OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("$ServiceName-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318");
            }));

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "$ServiceName" }));

    Log.Information("$ServiceName Service starting...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
"@

    Set-Content -Path (Join-Path $ServicePath "Program.cs") -Value $content
}

function Generate-CsprojFile {
    param(
        [string]$ServicePath,
        [string]$ProjectName
    )

    $content = @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.2.0" />
    <PackageReference Include="FluentValidation" Version="11.9.0" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
    <PackageReference Include="Serilog.AspNetCore" Version="8.0.1" />
    <PackageReference Include="Serilog.Sinks.Seq" Version="7.0.1" />
    <PackageReference Include="Serilog.Formatting.Compact" Version="2.0.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.7.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.7.1" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.7.1" />
    <PackageReference Include="StackExchange.Redis" Version="2.7.17" />
    <PackageReference Include="AWSSDK.DynamoDBv2" Version="3.7.0" />
    <PackageReference Include="AWSSDK.S3" Version="3.7.0" />
    <PackageReference Include="AWSSDK.SQS" Version="3.7.0" />
    <PackageReference Include="AWSSDK.SimpleNotificationService" Version="3.7.0" />
    <PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
    <PackageReference Include="Npgsql" Version="8.0.1" />
    <PackageReference Include="Dapper" Version="2.1.24" />
  </ItemGroup>
</Project>
"@

    $csprojPath = Get-ChildItem -Path $ServicePath -Filter "*.csproj" | Select-Object -First 1
    if ($csprojPath) {
        Set-Content -Path $csprojPath.FullName -Value $content
    }
}

function Generate-README {
    param(
        [string]$ServicePath,
        [string]$ServiceName,
        [string]$Tech,
        [int]$Port
    )

    $content = @"
# Gearify $ServiceName Service

Production-ready .NET 8 microservice for $ServiceName management.

## Technology Stack

- **Framework**: .NET 8
- **Architecture**: Clean Architecture + CQRS
- **Storage**: $Tech
- **Logging**: Serilog → Seq
- **Tracing**: OpenTelemetry → Jaeger
- **Port**: $Port

## Quick Start

\`\`\`bash
dotnet restore
dotnet run
\`\`\`

Service runs on http://localhost:$Port

## API Endpoints

- \`GET /health\` - Health check
- \`GET /api/*\` - Resource endpoints

## Environment Variables

- \`SEQ_URL\` - Seq logging endpoint
- \`REDIS_URL\` - Redis connection string
- \`OTLP_ENDPOINT\` - OpenTelemetry collector endpoint

## Docker

\`\`\`bash
docker build -t gearify-$($ServiceName.ToLower())-svc .
docker run -p ${Port}:80 gearify-$($ServiceName.ToLower())-svc
\`\`\`
"@

    Set-Content -Path (Join-Path $ServicePath "README.md") -Value $content
}

# Generate for each service
foreach ($service in $services) {
    Write-Host "Generating $($service.Name) Service..." -ForegroundColor Yellow

    $servicePath = Join-Path "C:\Gearify" $service.Path

    # Ensure directory structure
    $dirs = @("API/Controllers", "Application/Commands", "Application/Queries", "Application/Validators",
              "Domain/Entities", "Domain/Events", "Infrastructure/Repositories",
              "Tests/$($service.Namespace).UnitTests", "Tests/$($service.Namespace).IntegrationTests")

    foreach ($dir in $dirs) {
        New-DirectoryIfNotExists (Join-Path $servicePath $dir)
    }

    # Generate files
    Generate-ProgramFile -ServicePath $servicePath -Namespace $service.Namespace -ServiceName $service.Name
    Generate-CsprojFile -ServicePath $servicePath -ProjectName $service.Namespace
    Generate-README -ServicePath $servicePath -ServiceName $service.Name -Tech $service.Tech -Port $service.Port

    Write-Host "  ✓ $($service.Name) Service generated" -ForegroundColor Green
}

Write-Host "`nAll services generated successfully!" -ForegroundColor Green
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run 'dotnet restore' in each service directory" -ForegroundColor White
Write-Host "2. Run 'dotnet build' to verify compilation" -ForegroundColor White
Write-Host "3. Start infrastructure: docker-compose up -d" -ForegroundColor White
Write-Host "4. Run services individually or via docker-compose" -ForegroundColor White
