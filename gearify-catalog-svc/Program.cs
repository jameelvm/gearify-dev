using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Application.Validators;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.CatalogService.Infrastructure.Swagger;
using Gearify.SharedKernel.Extensions;
using MediatR;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Catalog Service API",
            Version = "v1",
            Description = "Gearify Catalog Service - Manages product catalog"
        });

        // Add X-Tenant-Id header parameter for all operations
        c.OperationFilter<TenantHeaderOperationFilter>();
    });

    // Add multitenancy support
    builder.Services.AddMultitenancy();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

    // MediatR
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

    // FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

    // AWS Services - Configure for LocalStack with fake credentials
    var dynamoEndpoint = builder.Configuration["DYNAMODB_ENDPOINT"] ?? "http://localhost:4566";
    var s3Endpoint = builder.Configuration["S3_ENDPOINT"] ?? "http://localhost:4566";

    // Use fake credentials for LocalStack (it doesn't validate them)
    var credentials = new BasicAWSCredentials("test", "test");

    var dynamoConfig = new AmazonDynamoDBConfig
    {
        ServiceURL = dynamoEndpoint
    };
    builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(credentials, dynamoConfig));

    var s3Config = new AmazonS3Config
    {
        ServiceURL = s3Endpoint,
        ForcePathStyle = true
    };
    builder.Services.AddSingleton<IAmazonS3>(new AmazonS3Client(credentials, s3Config));

    // Repositories
    builder.Services.AddScoped<IProductRepository, DynamoDbProductRepository>();

    // OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("catalog-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318");
            }));

    var app = builder.Build();

    // Add tenant resolution middleware (must be before controllers)
    app.UseMultitenancy();

    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "catalog" }));

    Log.Information("Catalog Service starting...");
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
