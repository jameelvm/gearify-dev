using Amazon.DynamoDBv2;
using Gearify.TenantService.Infrastructure.Repositories;
using Gearify.SharedKernel.Swagger;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Gearify.TenantService;

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
                Title = "Tenant Service API",
                Version = "v1",
                Description = "Gearify Tenant Service - Manages tenant configurations"
            });

            c.OperationFilter<TenantHeaderOperationFilter>();
        });

        // AWS Service Configuration
        services.AddLocalStack(Configuration);
        var awsOptions = Configuration.GetAWSOptions();
        services.AddDefaultAWSOptions(awsOptions);
        services.AddAWSService<IAmazonDynamoDB>();

        // Repositories
        services.AddScoped<ITenantRepository, DynamoDbTenantRepository>();
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
            endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "tenant" }));
        });
    }
}
