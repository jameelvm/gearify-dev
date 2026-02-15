using System;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Extensions;
using Gearify.SharedKernel.Messaging;
using Gearify.SharedKernel.Messaging.Idempotency;
using Gearify.SharedKernel.Swagger;
using StackExchange.Redis;
using Gearify.ShippingService.Infrastructure.Adapters;
using Gearify.ShippingService.Infrastructure.Configuration;
using Gearify.ShippingService.Infrastructure.Messaging;
using Gearify.ShippingService.Infrastructure.Messaging.Events.Inbound;
using Gearify.ShippingService.Infrastructure.Messaging.Handlers;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Gearify.ShippingService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Configuration
        services.Configure<MessagingConfiguration>(Configuration.GetSection("MessagingConfiguration"));

        // Redis for idempotency
        var redisConnectionString = Configuration.GetValue<string>("Redis:ConnectionString")
            ?? System.Environment.GetEnvironmentVariable("REDIS_URL")
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        // SQS Message Idempotency - prevents duplicate event processing
        services.AddRedisIdempotency(
            ttl: TimeSpan.FromDays(7),
            keyPrefix: "shipping-svc:idempotency:");

        // Controllers
        services.AddControllers();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Shipping Service API",
                Version = "v1",
                Description = "Gearify Shipping Service - Manages shipping and logistics"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // Shipping Adapters
        services.AddSingleton<IShippingAdapter, EasyPostAdapter>();
        services.AddSingleton<IShippingAdapter, ShippoAdapter>();
        services.AddSingleton<ShippingAggregator>();

        // AWS SNS Client
        var snsConfig = Configuration.GetSection("MessagingConfiguration:SNS").Get<SnsConfiguration>() ?? new SnsConfiguration();
        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = System.Environment.GetEnvironmentVariable("SNS_ENDPOINT")
                    ?? System.Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")
                    ?? "http://localhost:4566",
                AuthenticationRegion = snsConfig.Region
            };
            return new AmazonSimpleNotificationServiceClient(config);
        });
        services.AddScoped<ISnsEventPublisher, SnsEventPublisher>();

        // AWS SQS Client
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = System.Environment.GetEnvironmentVariable("SQS_ENDPOINT")
                    ?? System.Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")
                    ?? "http://localhost:4566",
                AuthenticationRegion = snsConfig.Region
            };
            return new AmazonSQSClient(config);
        });

        // Event Queue Processors - One queue per event type
        var messagingConfig = Configuration.GetSection("MessagingConfiguration").Get<MessagingConfiguration>()
            ?? new MessagingConfiguration();

        // OrderConfirmedEvent -> Create shipment and publish shipping events
        services.AddEventQueueProcessor<OrderConfirmedEvent, OrderConfirmedEventHandler>(
            messagingConfig.SQS.OrderConfirmedQueueUrl);

        // Health checks
        services.AddHealthChecks();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Correlation ID tracking (must be early in pipeline)
        app.UseCorrelation();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        // Routing
        app.UseRouting();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("/health");
            endpoints.MapGet("/", () => Results.Ok(new { service = "shipping-svc", status = "running" }));
        });
    }
}
