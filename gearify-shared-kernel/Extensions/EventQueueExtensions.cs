using Amazon.SQS;
using Gearify.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gearify.SharedKernel.Extensions;

/// <summary>
/// Extension methods for registering event queue processors.
///
/// Pattern: One Queue Per Event Type
/// - Each event type has its own SQS queue
/// - Each event type has its own handler
/// - SNS subscription filter routes events to correct queue
/// - No code-level filtering needed
/// </summary>
public static class EventQueueExtensions
{
    /// <summary>
    /// Registers an event queue processor for a specific event type.
    ///
    /// This method wires up:
    /// - IEventQueue&lt;TEvent&gt; using SqsEventQueue&lt;TEvent&gt;
    /// - IEventHandler&lt;TEvent&gt; using the specified handler
    /// - EventQueueProcessor&lt;TEvent&gt; as a hosted background service
    ///
    /// Usage:
    /// <code>
    /// services.AddEventQueueProcessor&lt;OrderCreatedEvent, OrderCreatedEventHandler&gt;(
    ///     queueUrl: config.SQS.OrderCreatedQueueUrl);
    /// </code>
    /// </summary>
    /// <typeparam name="TEvent">The event DTO type</typeparam>
    /// <typeparam name="THandler">The handler implementation type</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="queueUrl">The SQS queue URL for this event type</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEventQueueProcessor<TEvent, THandler>(
        this IServiceCollection services,
        string queueUrl)
        where TEvent : class
        where THandler : class, IEventHandler<TEvent>
    {
        // Register the queue
        services.AddScoped<IEventQueue<TEvent>>(sp =>
        {
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var logger = sp.GetRequiredService<ILogger<SqsEventQueue<TEvent>>>();

            return new SqsEventQueue<TEvent>(sqsClient, queueUrl, logger);
        });

        // Register the handler
        services.AddScoped<IEventHandler<TEvent>, THandler>();

        // Register the background processor
        services.AddHostedService<EventQueueProcessor<TEvent>>();

        return services;
    }

    /// <summary>
    /// Registers an event queue processor using a configuration delegate.
    ///
    /// This overload allows getting the queue URL from configuration at runtime.
    ///
    /// Usage:
    /// <code>
    /// services.AddEventQueueProcessor&lt;OrderCreatedEvent, OrderCreatedEventHandler, MessagingConfiguration&gt;(
    ///     config => config.SQS.OrderCreatedQueueUrl);
    /// </code>
    /// </summary>
    /// <typeparam name="TEvent">The event DTO type</typeparam>
    /// <typeparam name="THandler">The handler implementation type</typeparam>
    /// <typeparam name="TConfig">The configuration type (must be registered in DI)</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="queueUrlSelector">Function to extract queue URL from configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEventQueueProcessor<TEvent, THandler, TConfig>(
        this IServiceCollection services,
        Func<TConfig, string> queueUrlSelector)
        where TEvent : class
        where THandler : class, IEventHandler<TEvent>
        where TConfig : class
    {
        // Register the queue with configuration lookup
        services.AddScoped<IEventQueue<TEvent>>(sp =>
        {
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var logger = sp.GetRequiredService<ILogger<SqsEventQueue<TEvent>>>();
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TConfig>>();

            var queueUrl = queueUrlSelector(config.Value);
            return new SqsEventQueue<TEvent>(sqsClient, queueUrl, logger);
        });

        // Register the handler
        services.AddScoped<IEventHandler<TEvent>, THandler>();

        // Register the background processor
        services.AddHostedService<EventQueueProcessor<TEvent>>();

        return services;
    }
}
