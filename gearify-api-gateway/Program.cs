using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
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

    // JWT Authentication (Cognito)
    var cognitoAuthority = builder.Configuration["Cognito:Authority"];
    var cognitoUserPoolId = builder.Configuration["Cognito:UserPoolId"];
    var cognitoRegion = builder.Configuration["Cognito:Region"];

    if (!string.IsNullOrEmpty(cognitoAuthority) && !string.IsNullOrEmpty(cognitoUserPoolId))
    {
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer(options =>
            {
                options.Authority = $"{cognitoAuthority}/{cognitoUserPoolId}";
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    ValidateLifetime = true
                };
            });
    }

    builder.Services.AddAuthorization();

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

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseRateLimiter();

    if (!string.IsNullOrEmpty(cognitoAuthority))
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

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
