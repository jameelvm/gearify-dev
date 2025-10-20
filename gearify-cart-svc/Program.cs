using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Gearify.CartService.Infrastructure.Swagger;
using Gearify.SharedKernel.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cart Service API",
        Version = "v1",
        Description = "Gearify Cart Service - Manages shopping carts"
    });

    // Add X-Tenant-Id header parameter for all operations
    c.OperationFilter<TenantHeaderOperationFilter>();
});

// Add multitenancy support
builder.Services.AddMultitenancy();

// Use fake credentials for LocalStack (it doesn't validate them)
var credentials = new BasicAWSCredentials("test", "test");

var dynamoConfig = new AmazonDynamoDBConfig
{
    ServiceURL = builder.Configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:4566"
};
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(credentials, dynamoConfig));
builder.Services.AddSingleton<IDynamoDBContext, DynamoDBContext>();

var redisConnection = builder.Configuration["REDIS_URL"] ?? builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
// Remove redis:// prefix if present
if (redisConnection.StartsWith("redis://"))
{
    redisConnection = redisConnection.Substring(8);
}

var configOptions = ConfigurationOptions.Parse(redisConnection);
configOptions.AbortOnConnectFail = false;
configOptions.ConnectRetry = 5;
configOptions.ConnectTimeout = 5000;

var redis = ConnectionMultiplexer.Connect(configOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

// Add tenant resolution middleware (must be before controllers)
app.UseMultitenancy();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
