using Amazon.DynamoDBv2;
using Amazon.Runtime;
using FluentValidation;
using Gearify.AuthService.Application.Commands;
using Gearify.AuthService.Application.Models;
using Gearify.AuthService.Application.Services;
using Gearify.AuthService.Application.Validators;
using Gearify.AuthService.Infrastructure.Clients;
using Gearify.AuthService.Infrastructure.Configuration;
using Gearify.AuthService.Infrastructure.Repositories;
using Gearify.AuthService.Infrastructure.Services;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Extensions;
using LocalStack.Client.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;

namespace Gearify.AuthService;

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

        // Swagger with JWT Bearer support
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Auth Service API",
                Version = "v1",
                Description = "Gearify Auth Service - User authentication and authorization"
            });

            // Add JWT Bearer authentication to Swagger
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
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

        // JWT Authentication
        var jwtSecret = Configuration["JwtSettings:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        var jwtIssuer = Configuration["JwtSettings:Issuer"] ?? "gearify-auth";
        var jwtAudience = Configuration["JwtSettings:Audience"] ?? "gearify-api";

        Console.WriteLine($"=== JWT Configuration ===");
        Console.WriteLine($"Issuer: {jwtIssuer}");
        Console.WriteLine($"Audience: {jwtAudience}");
        Console.WriteLine($"=========================");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();

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
            // SES moved to notification-svc
            // services.AddAWSService<Amazon.SimpleEmail.IAmazonSimpleEmailService>();
            services.AddAWSService<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>();

            Console.WriteLine("AWS services registered successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR configuring AWS services: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        // Configuration sections
        services.Configure<SecurityConfiguration>(Configuration.GetSection("Security"));
        services.Configure<StorageConfiguration>(Configuration.GetSection("StorageConfiguration"));

        // Repositories
        services.AddScoped<IUserRepository, DynamoDbUserRepository>();
        services.AddScoped<IUserSessionRepository, DynamoDbUserSessionRepository>();
        services.AddScoped<IAddressRepository, DynamoDbAddressRepository>();

        // Core Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtService, JwtService>();

        // Email Service - via Notification Service HTTP client
        services.AddHttpClient<IEmailService, NotificationServiceClient>(client =>
        {
            var notificationServiceUrl = Configuration["NotificationService:BaseUrl"] ?? "http://localhost:5010";
            client.BaseAddress = new Uri(notificationServiceUrl);
        });

        // Security Services
        services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();
        services.AddScoped<IAccountLockoutService, AccountLockoutService>();

        // Session Services
        services.AddScoped<ISessionService, SessionService>();

        // OpenTelemetry
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318";
        Console.WriteLine($"=== OpenTelemetry Configuration ===");
        Console.WriteLine($"OTLP Endpoint: {otlpEndpoint}");
        Console.WriteLine($"Service Name: auth-service");
        Console.WriteLine($"====================================");

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("auth-service"))
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
        // Correlation ID tracking (must be early in pipeline)
        app.UseCorrelation();

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

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "auth" }));
        });
    }
}
