using Gearify.ShippingService.Infrastructure.Adapters;
using Microsoft.AspNetCore.Http;
using Gearify.ShippingService.Infrastructure.Swagger;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Gearify.ShippingService;

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

        // Swagger
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Shipping Service API",
                Version = "v1",
                Description = "Gearify Shipping Service - Manages shipping and logistics"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly));

        // Shipping Adapters
        services.AddSingleton<IShippingAdapter, EasyPostAdapter>();
        services.AddSingleton<IShippingAdapter, ShippoAdapter>();
        services.AddSingleton<ShippingAggregator>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Swagger
        app.UseSwagger();
        app.UseSwaggerUI();

        // Routing
        app.UseRouting();

        // Endpoints
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "shipping" }));
        });
    }
}
