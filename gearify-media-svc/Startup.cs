using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Gearify.MediaService.Application.Services;
using Gearify.SharedKernel.Events;
using Gearify.MediaService.Infrastructure.Messaging;
using Gearify.MediaService.Infrastructure.Messaging.Events.Inbound;
using Gearify.SharedKernel.Messaging;
using Gearify.MediaService.Infrastructure.Repositories;
using Gearify.MediaService.Infrastructure.Storage;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Extensions;
using Gearify.SharedKernel.Multitenancy;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Gearify.MediaService;

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

        // Multi-tenancy
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddMultitenancy();

        // Configuration
        services.Configure<Infrastructure.Configuration.ProductImageUploadConfiguration>(Configuration.GetSection("ProductImageUploadConfiguration"));
        services.Configure<Infrastructure.Configuration.MessagingConfiguration>(Configuration.GetSection("MessagingConfiguration"));
        services.Configure<Infrastructure.Configuration.StorageConfiguration>(Configuration.GetSection("StorageConfiguration"));

        // AWS Services with LocalStack
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var endpoint = Environment.GetEnvironmentVariable("DYNAMODB_ENDPOINT") ?? Configuration["AWS:DynamoDB:ServiceUrl"];
            var config = new Amazon.DynamoDBv2.AmazonDynamoDBConfig
            {
                ServiceURL = endpoint
            };
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            return new Amazon.DynamoDBv2.AmazonDynamoDBClient(credentials, config);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? Configuration["AWS:S3:ServiceUrl"];
            var config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true
            };
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            return new Amazon.S3.AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
        {
            var endpoint = Environment.GetEnvironmentVariable("SNS_ENDPOINT") ?? "http://localstack:4566";
            var config = new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceConfig
            {
                ServiceURL = endpoint
            };
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            return new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient(credentials, config);
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var endpoint = Environment.GetEnvironmentVariable("SQS_ENDPOINT") ?? "http://localstack:4566";
            var config = new Amazon.SQS.AmazonSQSConfig
            {
                ServiceURL = endpoint
            };
            var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
            var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            return new Amazon.SQS.AmazonSQSClient(credentials, config);
        });

        // MediatR for CQRS
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // Application Services
        services.AddScoped<IImageProcessor, ImageProcessor>();
        services.AddScoped<IMediaUrlFactory, MediaUrlFactory>();

        // Infrastructure Services
        services.AddScoped<IStorageService, ProductImageS3StorageService>();
        services.AddScoped<IMediaRepository, DynamoDbMediaRepository>();

        // Messaging Services (for async image processing)
        services.AddScoped<ISnsEventPublisher, SnsEventPublisher>();
        services.AddScoped<IEventQueue<ImageProcessingEventMessage>, SqsImageProcessingQueue>();

        // Background Services
        services.AddHostedService<ImageProcessingQueueProcessor>();

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Media Service API",
                Version = "v1",
                Description = "Gearify Media Service - Manages media files and assets"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Tenant resolution middleware (must be before controllers)
        app.UseMultitenancy();

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
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "media" }));
        });
    }
}
