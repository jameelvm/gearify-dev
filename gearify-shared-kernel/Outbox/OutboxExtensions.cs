using Gearify.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gearify.SharedKernel.Outbox;

public static class OutboxExtensions
{
    /// <summary>
    /// Registers the OutboxPublisher, OutboxCleanupService, OutboxMessageFactory,
    /// and ITopicArnResolver for the given DbContext.
    /// </summary>
    public static IServiceCollection AddOutboxPublisher<TDbContext>(
        this IServiceCollection services,
        Action<OutboxPublisherOptions>? configureOptions = null)
        where TDbContext : DbContext
    {
        // Options
        if (configureOptions != null)
            services.Configure(configureOptions);
        else
            services.Configure<OutboxPublisherOptions>(_ => { });

        // ITopicArnResolver — resolved from the existing ISnsEventPublisher registration
        // (SnsEventPublisherBase implements ITopicArnResolver via explicit interface)
        services.AddScoped<ITopicArnResolver>(sp =>
            (ITopicArnResolver)sp.GetRequiredService<ISnsEventPublisher>());

        // Factory
        services.AddScoped<IOutboxMessageFactory, OutboxMessageFactory>();

        // Background services
        services.AddHostedService<OutboxPublisher<TDbContext>>();
        services.AddHostedService<OutboxCleanupService<TDbContext>>();

        return services;
    }
}
