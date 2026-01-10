using Amazon.Runtime;
using Amazon.SQS;
using Gearify.SearchService.Application.Events;
using Gearify.SearchService.Application.Services;
using Gearify.SearchService.Infrastructure.Configuration;
using Gearify.SearchService.Infrastructure.Messaging;
using Gearify.SearchService.Infrastructure.OpenSearch;
using Gearify.SearchService.Infrastructure.Swagger;
using Gearify.SharedKernel.Extensions;
using LocalStack.Client.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;

namespace Gearify.SearchService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Log configuration for debugging
        var useLocalStack = Configuration.GetValue<bool>("LocalStack:UseLocalStack");
        var localStackHost = Configuration["LocalStack:Config:LocalStackHost"];
        var openSearchEndpoint = Configuration["OpenSearch:Endpoint"];

        Console.WriteLine($"=== Search Service Configuration ===");
        Console.WriteLine($"UseLocalStack: {useLocalStack}");
        Console.WriteLine($"LocalStackHost: {localStackHost}");
        Console.WriteLine($"OpenSearch Endpoint: {openSearchEndpoint}");
        Console.WriteLine($"Environment: {Configuration["ASPNETCORE_ENVIRONMENT"]}");
        Console.WriteLine($"====================================");

        // Controllers
        services.AddControllers();
        services.AddEndpointsApiExplorer();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Search Service API",
                Version = "v1",
                Description = "Gearify Search Service - Product search with full-text search, autocomplete, and faceted filtering"
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

        // AWS Service Configuration
        try
        {
            var awsOptions = Configuration.GetAWSOptions();
            services.AddDefaultAWSOptions(awsOptions);
            services.AddLocalStack(Configuration);

            Console.WriteLine("AWS services registered successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR configuring AWS services: {ex.Message}");
            throw;
        }

        // OpenSearch Configuration
        services.Configure<OpenSearchSettings>(options =>
        {
            Configuration.GetSection("OpenSearch").Bind(options);

            // Override with environment variable if present
            var envEndpoint = Environment.GetEnvironmentVariable("OPENSEARCH_ENDPOINT");
            if (!string.IsNullOrEmpty(envEndpoint))
            {
                options.Endpoint = envEndpoint;
                Console.WriteLine($"OpenSearch Endpoint overridden from environment: {envEndpoint}");
            }
        });
        services.AddSingleton<IOpenSearchClientFactory, OpenSearchClientFactory>();
        services.AddSingleton<IIndexManager, IndexManager>();
        services.AddScoped<IProductIndexService, ProductIndexService>();

        Console.WriteLine("OpenSearch services registered successfully");

        // Messaging Configuration (SQS Consumer)
        services.Configure<MessagingSettings>(Configuration.GetSection("Messaging"));

        // Register SQS client with environment variable support
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var endpoint = Environment.GetEnvironmentVariable("SQS_ENDPOINT")
                ?? Configuration["AWS:SQS:ServiceUrl"]
                ?? "http://localhost:4566";
            var config = new AmazonSQSConfig
            {
                ServiceURL = endpoint
            };
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            Console.WriteLine($"SQS Endpoint configured: {endpoint}");
            return new AmazonSQSClient(credentials, config);
        });

        services.AddScoped<ICatalogEventHandler, CatalogEventHandler>();
        services.AddHostedService<CatalogEventConsumer>();

        Console.WriteLine("Messaging services registered successfully");

        // TODO: Module 4.3 - Add Redis cache configuration
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Tenant resolution middleware
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
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "search" }));
        });
    }
}
