using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Formatting.Json;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var dynamoConfig = new AmazonDynamoDBConfig
{
    ServiceURL = builder.Configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:8000"
};
builder.Services.AddSingleton<IAmazonDynamoDB>(new AmazonDynamoDBClient(dynamoConfig));
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
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();
