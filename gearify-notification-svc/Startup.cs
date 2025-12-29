using System;
using Amazon.Runtime;
using Gearify.NotificationService.Infrastructure.Email;
using Gearify.NotificationService.Infrastructure.Swagger;
using LocalStack.Client.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
