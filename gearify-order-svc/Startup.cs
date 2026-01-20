using System;
using Gearify.OrderService.Infrastructure.Configuration;
using Gearify.OrderService.Infrastructure.Data;
using Gearify.OrderService.Infrastructure.Repositories;
using Gearify.SharedKernel.Swagger;
using Gearify.SharedKernel.Multitenancy;
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

        services.AddDbContext<OrderDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(3);
                npgsqlOptions.CommandTimeout(30);
            });
        });

        // Repositories
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Multitenancy
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();

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
