using System;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Gearify.OrderService.Infrastructure.Configuration;
using Gearify.OrderService.Infrastructure.Data;
using Gearify.OrderService.Infrastructure.Messaging;
using Gearify.OrderService.Infrastructure.Messaging.Handlers;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.OrderService.Infrastructure.UnitOfWork;
using Gearify.OrderService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Messaging;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Gearify.OrderService;

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
        services.Configure<DatabaseConfiguration>(Configuration.GetSection("DatabaseConfiguration"));
        services.Configure<MessagingConfiguration>(Configuration.GetSection("MessagingConfiguration"));

        // Database
        var connectionString = Configuration.GetValue<string>("DatabaseConfiguration:ConnectionString")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=gearify_orders;Username=postgres;Password=postgres";

        // Use pooled DbContext factory - this allows both DI and factory pattern
        services.AddPooledDbContextFactory<OrderDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(30);
            });
        });

        // Register DbContext as scoped (created from factory)
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<OrderDbContext>>().CreateDbContext());

        // Repositories & Unit of Work
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();

        // Multitenancy
        services.AddHttpContextAccessor();
        services.AddMultitenancy();

        // AWS SNS Client
        var snsConfig = Configuration.GetSection("MessagingConfiguration:SNS").Get<SnsConfiguration>() ?? new SnsConfiguration();
        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = Environment.GetEnvironmentVariable("SNS_ENDPOINT")
                    ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")
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
                ServiceURL = Environment.GetEnvironmentVariable("SQS_ENDPOINT")
                    ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")
                    ?? "http://localhost:4566",
                AuthenticationRegion = snsConfig.Region
            };
            return new AmazonSQSClient(config);
        });

        // Event Queue Processors - One queue per event type
        // Pattern: SNS filters events to correct queue, handler processes single event type
        var messagingConfig = Configuration.GetSection("MessagingConfiguration").Get<MessagingConfiguration>()
            ?? new MessagingConfiguration();

        // PaymentCompletedEvent -> Confirm Order
        services.AddEventQueueProcessor<PaymentCompletedEvent, PaymentCompletedEventHandler>(
            messagingConfig.SQS.PaymentCompletedQueueUrl);

        // PaymentFailedEvent -> Mark Order as PaymentFailed
        services.AddEventQueueProcessor<PaymentFailedEvent, PaymentFailedEventHandler>(
            messagingConfig.SQS.PaymentFailedQueueUrl);

        // RefundCompletedEvent -> Mark Order as Refunded
        services.AddEventQueueProcessor<RefundCompletedEvent, RefundCompletedEventHandler>(
            messagingConfig.SQS.RefundCompletedQueueUrl);

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // Controllers
        services.AddControllers();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Order Service API",
                Version = "v1",
                Description = "Gearify Order Service - Manages orders and order processing"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // Health checks
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql");
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Ensure database is created (for development)
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            dbContext.Database.EnsureCreated();
        }

        // Tenant resolution middleware (must be before controllers)
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
            endpoints.MapHealthChecks("/health");
            endpoints.MapGet("/", () => Results.Ok(new { service = "order-svc", status = "running" }));
        });
    }
}
