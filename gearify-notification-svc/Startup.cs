using System;
using Amazon.SQS;
using Gearify.NotificationService.Infrastructure.Clients;
using Gearify.NotificationService.Infrastructure.Configuration;
using Gearify.NotificationService.Infrastructure.Email;
using Gearify.NotificationService.Infrastructure.Messaging;
using Gearify.SharedKernel.Messaging;
using Gearify.SharedKernel.Messaging.Idempotency;
using Gearify.SharedKernel.Swagger;
using StackExchange.Redis;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Gearify.NotificationService;

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

        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });

        // Redis for idempotency
        var redisConnectionString = Configuration.GetValue<string>("Redis:ConnectionString")
            ?? Environment.GetEnvironmentVariable("REDIS_URL")
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        // SQS Message Idempotency - prevents duplicate event processing
        services.AddRedisIdempotency(
            ttl: TimeSpan.FromDays(7),
            keyPrefix: "notification-svc:idempotency:");

        // AWS Services with LocalStack
        var awsOptions = Configuration.GetAWSOptions();

        // Override ServiceURL from environment variable if present (for Docker)
        var sesEndpoint = Environment.GetEnvironmentVariable("SES_ENDPOINT");
        if (!string.IsNullOrEmpty(sesEndpoint))
        {
            awsOptions.DefaultClientConfig.ServiceURL = sesEndpoint;
            Console.WriteLine($"Overriding AWS ServiceURL from environment: {sesEndpoint}");
        }

        Console.WriteLine($"AWS Options - Region: {awsOptions.Region}");
        Console.WriteLine($"AWS Options - ServiceURL: {awsOptions.DefaultClientConfig.ServiceURL}");

        services.AddDefaultAWSOptions(awsOptions);
        services.AddLocalStack(Configuration);
        services.AddAWSService<Amazon.SimpleEmail.IAmazonSimpleEmailService>();

        // Email Services
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailService, SesEmailService>();

        // Configuration
        services.Configure<MessagingConfiguration>(Configuration.GetSection("MessagingConfiguration"));

        // AWS SQS Client
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var config = new AmazonSQSConfig
            {
                ServiceURL = Environment.GetEnvironmentVariable("SQS_ENDPOINT")
                    ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL")
                    ?? "http://localhost:4566",
                AuthenticationRegion = "us-east-1"
            };
            return new AmazonSQSClient(config);
        });

        // Event Queue Consumer (Payment Events - Completed & Failed)
        services.AddScoped<IEventQueue<PaymentEventMessage>>(sp =>
        {
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var config = sp.GetRequiredService<IOptions<MessagingConfiguration>>();
            var logger = sp.GetRequiredService<ILogger<SqsEventQueue<PaymentEventMessage>>>();

            return new SqsEventQueue<PaymentEventMessage>(
                sqsClient,
                config.Value.SQS.PaymentEventsQueueUrl,
                logger,
                eventTypeFilters: ["PaymentCompletedEvent", "PaymentFailedEvent"],
                eventTypeEnricher: (msg, type) => msg with { EventType = type });
        });
        services.AddScoped<IEventHandler<PaymentEventMessage>, PaymentEventHandler>();
        services.AddHostedService<EventQueueProcessor<PaymentEventMessage>>();

        // Event Queue Consumer (Refund Events - Completed & Failed)
        services.AddScoped<IEventQueue<RefundEventMessage>>(sp =>
        {
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var config = sp.GetRequiredService<IOptions<MessagingConfiguration>>();
            var logger = sp.GetRequiredService<ILogger<SqsEventQueue<RefundEventMessage>>>();

            return new SqsEventQueue<RefundEventMessage>(
                sqsClient,
                config.Value.SQS.RefundEventsQueueUrl,
                logger,
                eventTypeFilters: ["RefundCompletedEvent", "RefundFailedEvent"],
                eventTypeEnricher: (msg, type) => msg with { EventType = type });
        });
        services.AddScoped<IEventHandler<RefundEventMessage>, RefundEventHandler>();
        services.AddHostedService<EventQueueProcessor<RefundEventMessage>>();

        // Auth Service Client
        services.AddHttpClient<IAuthServiceClient, AuthServiceClient>(client =>
        {
            var authServiceUrl = Configuration["AuthService:BaseUrl"] ?? "http://localhost:5001";
            client.BaseAddress = new Uri(authServiceUrl);
        });

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Notification Service API",
                Version = "v1",
                Description = "Gearify Notification Service - Manages notifications and email notifications"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // CORS
        app.UseCors();

        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        // Routing
        app.UseRouting();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "notification" }));
        });
    }
}
