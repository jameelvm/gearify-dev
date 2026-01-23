using System;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Gearify.PaymentService.Infrastructure.Configuration;
using Gearify.PaymentService.Infrastructure.Data;
using Gearify.PaymentService.Infrastructure.Messaging;
using Gearify.PaymentService.Infrastructure.PaymentProviders;
using Gearify.PaymentService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Events;
using Gearify.SharedKernel.Messaging;
using Gearify.PaymentService.Infrastructure.Repositories;
using Gearify.PaymentService.Infrastructure.UnitOfWork;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace Gearify.PaymentService;

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
        services.Configure<StripeConfiguration>(Configuration.GetSection("Stripe"));
        services.Configure<MessagingConfiguration>(Configuration.GetSection("MessagingConfiguration"));
        services.Configure<PaymentProviderConfiguration>(Configuration.GetSection("PaymentProvider"));

        // Database - Entity Framework Core
        var connectionString = Configuration.GetConnectionString("PaymentDb")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=gearify_payments;Username=postgres;Password=postgres";

        // Use pooled DbContext factory - this allows both DI and factory pattern
        services.AddPooledDbContextFactory<PaymentDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(30);
            });
        });

        // Register DbContext as scoped (created from factory)
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<PaymentDbContext>>().CreateDbContext());

        // Redis for idempotency and caching
        var redisConnectionString = Configuration.GetValue<string>("Redis:ConnectionString")
            ?? Environment.GetEnvironmentVariable("REDIS_URL")
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        // Repositories & Unit of Work
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();

        // Payment Providers - conditionally use mock or real
        var useMockProviders = Configuration.GetValue<bool>("PaymentProvider:UseMockProviders", true);

        if (useMockProviders)
        {
            // Mock providers for local development and testing
            services.AddScoped<IStripePaymentProvider, MockStripePaymentProvider>();
            services.AddScoped<IPayPalPaymentProvider, MockPayPalPaymentProvider>();
        }
        else
        {
            // Real providers for staging/production
            services.AddScoped<IStripePaymentProvider, StripePaymentProvider>();
            services.AddHttpClient<IPayPalPaymentProvider, PayPalPaymentProvider>();
        }

        services.AddScoped<IIdempotencyService, RedisIdempotencyService>();

        // Multitenancy
        services.AddHttpContextAccessor();
        services.AddMultitenancy();

        // AWS SNS Client
        var snsConfig = Configuration.GetSection("MessagingConfiguration:SNS").Get<SnsConfiguration>() ?? new SnsConfiguration();
        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var config = new AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566",
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
                ServiceURL = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566",
                AuthenticationRegion = snsConfig.Region
            };
            return new AmazonSQSClient(config);
        });

        // Messaging Services (for receiving order events)
        services.AddScoped<IEventQueue<OrderCreatedEventMessage>, SqsOrderEventQueue>();

        // Background services
        services.AddHostedService<OrderEventQueueProcessor>();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // Controllers
        services.AddControllers();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Payment Service API",
                Version = "v1",
                Description = "Gearify Payment Service - Manages payment processing with Stripe and PayPal"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // Health checks
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddRedis(redisConnectionString, name: "redis");
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Ensure database is created (for development)
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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
            endpoints.MapGet("/", () => Results.Ok(new { service = "payment-svc", status = "running" }));
        });
    }
}
