using Amazon.DynamoDBv2;
using Amazon.SQS;
using Gearify.ApiGateway.Middleware;
using Gearify.SharedKernel.AI;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:80")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // CORS - Allow subdomain-based origins
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.SetIsOriginAllowed(origin =>
            {
                // Allow localhost variations
                if (origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
                    return true;

                // Allow localhost.direct subdomains
                if (origin.Contains("localhost.direct:"))
                    return true;

                // Allow localtest.me subdomains
                if (origin.Contains("localtest.me:"))
                    return true;

                // Allow production domains (add your production domain here)
                if (origin.Contains("gearify.com"))
                    return true;

                return false;
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
    });

    // YARP Reverse Proxy
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    // Rate Limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var tenantId = context.Request.Headers["X-Tenant-Id"].ToString() ?? "anonymous";

            return RateLimitPartition.GetFixedWindowLimiter(tenantId, _ =>
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 100),
                    Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:Window", 60)),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // JWT Authentication (Auth Service)
    var jwtSecret = builder.Configuration["JwtSettings:Secret"];
    var jwtIssuer = builder.Configuration["JwtSettings:Issuer"];
    var jwtAudience = builder.Configuration["JwtSettings:Audience"];

    if (!string.IsNullOrEmpty(jwtSecret))
    {
        var key = System.Text.Encoding.UTF8.GetBytes(jwtSecret);

        builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateLifetime = true,        // Validates token expiration
                    ClockSkew = TimeSpan.Zero       // No grace period for expiration
                };

                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException)
                        {
                            context.Response.Headers.Append("Token-Expired", "true");
                            Log.Warning("JWT token expired for request {Path}", context.Request.Path);
                        }
                        else
                        {
                            Log.Error(context.Exception, "JWT authentication failed for request {Path}", context.Request.Path);
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        var tenantId = context.Principal?.FindFirst("tenantId")?.Value;
                        Log.Information("JWT token validated for user {UserId} in tenant {TenantId}", userId, tenantId);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        Log.Warning("JWT authentication challenge for request {Path}: {Error}",
                            context.Request.Path, context.ErrorDescription);
                        return Task.CompletedTask;
                    }
                };
            });
    }
    else
    {
        Log.Warning("JWT authentication is disabled: No JWT secret configured");
    }

    builder.Services.AddAuthorization();

    // AWS SQS + DynamoDB for event tracking
    var awsEndpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT")
                      ?? builder.Configuration["AI:LocalStackEndpoint"];
    var useLocalStack = !string.IsNullOrEmpty(awsEndpoint);

    builder.Services.AddSingleton<IAmazonSQS>(_ =>
    {
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                builder.Configuration["AI:Region"] ?? "us-east-1")
        };
        if (useLocalStack)
        {
            config.ServiceURL = awsEndpoint;
            config.AuthenticationRegion = "us-east-1";
        }
        return new AmazonSQSClient(config);
    });

    builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
    {
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                builder.Configuration["AI:Region"] ?? "us-east-1")
        };
        if (useLocalStack)
        {
            config.ServiceURL = awsEndpoint;
            config.AuthenticationRegion = "us-east-1";
        }
        return new AmazonDynamoDBClient(config);
    });

    // User interaction event tracking (SQS publisher + background processor)
    builder.Services.AddUserInteractionTracking(builder.Configuration);

    // OpenTelemetry
    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("api-gateway"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTLP_ENDPOINT") ?? "http://otel-collector:4318");
            }));

    var app = builder.Build();

    // Correlation ID tracking (must be early in pipeline)
    app.UseMiddleware<CorrelationMiddleware>();

    app.UseSerilogRequestLogging();
    app.UseCors();

    // Tenant resolution middleware - must be before rate limiter and auth
    app.UseMiddleware<TenantResolutionMiddleware>();

    app.UseRateLimiter();

    // Always enable authentication and authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Event tracking — after auth so user identity is available
    app.UseMiddleware<EventTrackingMiddleware>();

    app.MapReverseProxy();
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        service = "api-gateway",
        timestamp = DateTime.UtcNow
    }));

    Log.Information("API Gateway starting...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "API Gateway failed to start");
}
finally
{
    Log.CloseAndFlush();
}
