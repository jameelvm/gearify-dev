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

        // TODO: Module 1.2 - Add OpenSearch client configuration
        // TODO: Module 4.3 - Add Redis cache configuration
        // TODO: Module 3.2 - Add SQS consumer for catalog events
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
