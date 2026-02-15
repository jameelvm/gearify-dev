using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gearify.SharedKernel.Outbox;

/// <summary>
/// Background worker that polls the outbox_messages table and publishes to SNS.
/// Generic on TDbContext so each service runs its own instance against its own database.
/// </summary>
public class OutboxPublisher<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly OutboxPublisherOptions _options;
    private readonly ILogger<OutboxPublisher<TDbContext>> _logger;

    public OutboxPublisher(
        IServiceScopeFactory scopeFactory,
        IAmazonSimpleNotificationService snsClient,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxPublisher<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _snsClient = snsClient;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisher<{DbContext}> started. Polling every {Interval}s, batch size {BatchSize}",
            typeof(TDbContext).Name, _options.PollingInterval.TotalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in OutboxPublisher polling loop");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.PublishedAt == null
                && (m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow)
                && m.RetryCount < _options.MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);

        if (messages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var request = new PublishRequest
                {
                    TopicArn = message.TopicArn,
                    Message = message.Payload,
                    Subject = message.EventType,
                    MessageAttributes = DeserializeMessageAttributes(message.MessageAttributes)
                };

                var response = await _snsClient.PublishAsync(request, ct);

                message.PublishedAt = DateTime.UtcNow;

                _logger.LogDebug("Published outbox message {MessageId} ({EventType}) to {TopicArn}. SNS MessageId: {SnsMessageId}",
                    message.Id, message.EventType, message.TopicArn, response.MessageId);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message;
                message.NextRetryAt = DateTime.UtcNow.AddSeconds(
                    Math.Pow(_options.BackoffBase, message.RetryCount));

                _logger.LogWarning(ex,
                    "Failed to publish outbox message {MessageId} ({EventType}). Retry {RetryCount}/{MaxRetries}, next retry at {NextRetryAt}",
                    message.Id, message.EventType, message.RetryCount, _options.MaxRetries, message.NextRetryAt);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private static Dictionary<string, MessageAttributeValue> DeserializeMessageAttributes(string? json)
    {
        var result = new Dictionary<string, MessageAttributeValue>();

        if (string.IsNullOrEmpty(json))
            return result;

        var attributes = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (attributes == null)
            return result;

        foreach (var (key, value) in attributes)
        {
            result[key] = new MessageAttributeValue
            {
                DataType = "String",
                StringValue = value
            };
        }

        return result;
    }
}
