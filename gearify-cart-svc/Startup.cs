using System;
using Amazon.DynamoDBv2;
using Gearify.CartService.Infrastructure.Caching;
using Gearify.CartService.Infrastructure.Clients;
using Gearify.CartService.Infrastructure.Configuration;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Extensions;
using LocalStack.Client.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace Gearify.CartService;

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

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cart Service API",
                Version = "v1",
                Description = "Gearify Cart Service - Manages shopping carts"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // Multitenancy
        services.AddMultitenancy();

        // LocalStack Configuration (Development only)
        services.AddLocalStack(Configuration);

        // AWS Service Configuration
        var awsOptions = Configuration.GetAWSOptions();

        // Override ServiceURL from environment variable if present (for Docker/LocalStack)
        var dynamoDbEndpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT");
        if (!string.IsNullOrEmpty(dynamoDbEndpoint))
        {
            awsOptions.DefaultClientConfig.ServiceURL = dynamoDbEndpoint;
            Console.WriteLine($"[Cart Service] Using DynamoDB endpoint: {dynamoDbEndpoint}");
        }

        services.AddDefaultAWSOptions(awsOptions);
        services.AddAWSService<IAmazonDynamoDB>();

        // Cart Configuration
        services.Configure<CartConfiguration>(Configuration.GetSection(CartConfiguration.SectionName));

        // Redis - Using factory pattern
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisConnection = Configuration["REDIS_URL"] ?? Configuration["Redis:ConnectionString"] ?? "localhost:6379";
            if (redisConnection.StartsWith("redis://"))
            {
                redisConnection = redisConnection.Substring(8);
            }

            var configOptions = ConfigurationOptions.Parse(redisConnection);
            configOptions.AbortOnConnectFail = false;
            configOptions.ConnectRetry = 5;
            configOptions.ConnectTimeout = 5000;

            return ConnectionMultiplexer.Connect(configOptions);
        });

        // Cache Services
        services.AddSingleton<ICacheService, RedisCacheService>();
        services.AddSingleton<ICartCacheService, CartCacheService>();

        // Cart Repository (DynamoDB persistence)
        services.AddScoped<ICartRepository, DynamoDbCartRepository>();

        // Catalog Service Client
        services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Correlation ID tracking (must be early in pipeline)
        app.UseCorrelation();

        // Tenant resolution middleware
        app.UseMultitenancy();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        // Routing
        app.UseRouting();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "cart" }));
        });
    }
}
