using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Gearify.CartService.Infrastructure.Clients;
using Gearify.CartService.Infrastructure.Repositories;
using Gearify.CartService.Infrastructure.Swagger;
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
        services.AddDefaultAWSOptions(Configuration.GetAWSOptions());
        services.AddAWSService<IAmazonDynamoDB>();

        // DynamoDB Context
        services.AddSingleton<IDynamoDBContext>(sp =>
        {
            var dynamoClient = sp.GetRequiredService<IAmazonDynamoDB>();
            return new DynamoDBContext(dynamoClient);
        });

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

        // Cart Repository
        services.AddScoped<ICartRepository, RedisCartRepository>();

        // Catalog Service Client
        services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
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
