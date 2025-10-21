using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Application.Validators;
using Gearify.CatalogService.Infrastructure.Repositories;
using Gearify.CatalogService.Infrastructure.Swagger;
using Gearify.SharedKernel.Extensions;
using LocalStack.Client.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Gearify.CatalogService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Controllers
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Catalog Service API",
                Version = "v1",
                Description = "Gearify Catalog Service - Manages product catalog"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // Multitenancy
        services.AddMultitenancy();

        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<CreateProductValidator>();

        // LocalStack Configuration (Development only)
        // Automatically configures AWS services to use LocalStack when enabled
        services.AddLocalStack(Configuration);

        // AWS Service Configuration
        // Uses LocalStack in development, IAM roles in production
        services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
        services.AddAWSService<IAmazonDynamoDB>();
        services.AddAWSService<IAmazonS3>();

        // Repositories
        services.AddScoped<IProductRepository, DynamoDbProductRepository>();

        // OpenTelemetry
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("catalog-service"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318");
                }));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Tenant resolution middleware (must be before controllers)
        app.UseMultitenancy();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        // Request logging
        app.UseSerilogRequestLogging();

        // CORS
        app.UseCors();

        // Routing
        app.UseRouting();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "catalog" }));
        });
    }
}
