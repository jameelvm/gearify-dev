using Gearify.SharedKernel.AI.Caching;
using Gearify.SharedKernel.AI.Events;
using Gearify.SharedKernel.AI.Monitoring;
using Gearify.SharedKernel.AI.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gearify.SharedKernel.AI;

public static class AIServiceExtensions
{
    public static IServiceCollection AddAIInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AIServiceConfiguration>(configuration.GetSection("AI"));

        services.AddSingleton<AICircuitBreakerPolicy>();
        services.AddSingleton<BedrockCostTracker>();
        services.AddScoped<IAICacheService, RedisCacheService>();

        return services;
    }

    /// <summary>
    /// Registers the user interaction event tracking pipeline (SQS publisher + background processor).
    /// Call this in services that need to publish or consume user interaction events.
    /// </summary>
    public static IServiceCollection AddUserInteractionTracking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AIServiceConfiguration>(configuration.GetSection("AI"));
        services.AddSingleton<IUserInteractionPublisher, SqsUserInteractionPublisher>();
        services.AddHostedService<UserInteractionProcessor>();

        return services;
    }
}
