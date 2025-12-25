using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Gearify.CatalogService.Application.Commands;
using Gearify.CatalogService.Application.Mappers;
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
        // Log LocalStack configuration for debugging
        var useLocalStack = Configuration.GetValue<bool>("LocalStack:UseLocalStack");
        var localStackHost = Configuration["LocalStack:Config:LocalStackHost"];
        var awsRegion = Configuration["AWS:Region"];

        Console.WriteLine($"=== LocalStack Configuration ===");
        Console.WriteLine($"UseLocalStack: {useLocalStack}");
        Console.WriteLine($"LocalStackHost: {localStackHost}");
        Console.WriteLine($"AWS Region: {awsRegion}");
        Console.WriteLine($"Environment: {Configuration["ASPNETCORE_ENVIRONMENT"]}");
        Console.WriteLine($"================================");

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

        // AWS Service Configuration
        try
        {
            var awsOptions = Configuration.GetAWSOptions();

            // Override ServiceURL from environment variable if present (for Docker)
            var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
            if (!string.IsNullOrEmpty(dynamoDbEndpoint))
            {
                awsOptions.DefaultClientConfig.ServiceURL = dynamoDbEndpoint;
                Console.WriteLine($"Overriding AWS ServiceURL from environment: {dynamoDbEndpoint}");
            }

            Console.WriteLine($"AWS Options - Region: {awsOptions.Region}");
            Console.WriteLine($"AWS Options - ServiceURL: {awsOptions.DefaultClientConfig.ServiceURL}");

            services.AddDefaultAWSOptions(awsOptions);

            // AddLocalStack for credentials/configuration (must be after AddDefaultAWSOptions)
            services.AddLocalStack(Configuration);

            services.AddAWSService<IAmazonDynamoDB>();
            services.AddAWSService<IAmazonS3>();

            Console.WriteLine("AWS services registered successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR configuring AWS services: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Repositories
        services.AddScoped<IProductRepository, DynamoDbProductRepository>();
        services.AddScoped<ICategoryRepository, DynamoDbCategoryRepository>();
        services.AddScoped<IBrandRepository, DynamoDbBrandRepository>();
        services.AddScoped<IPriceRangeRepository, DynamoDbPriceRangeRepository>();

        // Section Mappers (Strategy Pattern)
        services.AddScoped<ISectionMapper, BrandSectionMapper>();
        services.AddScoped<ISectionMapperFactory, SectionMapperFactory>();

        // OpenTelemetry
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318";
        Console.WriteLine($"=== OpenTelemetry Configuration ===");
        Console.WriteLine($"OTLP Endpoint: {otlpEndpoint}");
        Console.WriteLine($"Service Name: catalog-service");
        Console.WriteLine($"====================================");

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("catalog-service"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
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
