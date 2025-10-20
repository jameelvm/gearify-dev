using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Gearify.SearchService.Infrastructure.Swagger;

/// <summary>
/// Swagger operation filter that adds X-Tenant-Id header to all API operations
/// </summary>
public class TenantHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Skip health and swagger endpoints
        var path = context.ApiDescription.RelativePath?.ToLower() ?? "";
        if (path.StartsWith("health") || path.StartsWith("swagger"))
        {
            return;
        }

        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Tenant-Id",
            In = ParameterLocation.Header,
            Description = "Tenant identifier for multi-tenancy support",
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new Microsoft.OpenApi.Any.OpenApiString("default-tenant")
            }
        });
    }
}
